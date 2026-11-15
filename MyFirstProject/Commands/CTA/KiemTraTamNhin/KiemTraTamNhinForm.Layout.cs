using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsColor = System.Drawing.Color;
using WinFormsFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    public partial class KiemTraTamNhinForm : Form
    {
        /// <summary>Initializes the existing controls in their original creation order.</summary>
        private void InitializeCustomComponents()
        {
            this.Text = "Kiểm Tra & Phân Tích Tầm Nhìn Xe Chạy (Sight Distance Analysis) - TCVN / AASHTO";
            this.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1160, 740);
            this.Size = (_lastFormSize.Width < 1160 || _lastFormSize.Height < 740) ? new Size(1180, 780) : _lastFormSize;
            BuildHeader();
            BuildBottomActions();
            TableLayoutPanel topTable = BuildTopContainer();
            BuildSourceGroup(topTable);
            BuildStandardGroup(topTable);
            BuildOptionsGroup(topTable);
            BuildResultsGroup();
        }

        /// <summary>Builds the original header controls.</summary>
        private void BuildHeader()
        {
            // 1. Panel Header trên cùng
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = WinFormsColor.FromArgb(28, 64, 112)
            };

            var lblTitle = new WinFormsLabel
            {
                Text = "KIỂM TRA & ĐÁNH GIÁ TẦM NHÌN XE CHẠY (SIGHT DISTANCE)",
                ForeColor = WinFormsColor.White,
                Font = new WinFormsFont("Segoe UI", 12.0f, FontStyle.Bold),
                Location = new WinFormsPoint(16, 8),
                AutoSize = true
            };

            var lblSub = new WinFormsLabel
            {
                Text = "Hỗ trợ Tuyến Civil 3D (Alignment) & Đường Polyline 2D/3D | TCVN 4054, 13592, 5729, 10380 & AASHTO",
                ForeColor = WinFormsColor.FromArgb(200, 220, 245),
                Font = new WinFormsFont("Segoe UI", 8.5f, FontStyle.Regular),
                Location = new WinFormsPoint(17, 32),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            this.Controls.Add(pnlHeader);
        }

        /// <summary>Builds the original action buttons and summary.</summary>
        private void BuildBottomActions()
        {
            // 2. Panel Bottom dưới cùng (Chứa nút bấm & Thống kê)
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = WinFormsColor.FromArgb(245, 247, 250),
                Padding = new Padding(12, 8, 12, 8)
            };
            this.Controls.Add(pnlBottom);

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false
            };
            pnlBottom.Controls.Add(pnlButtons);

            btnClose = new Button { Text = "Đóng", Width = 75, Height = 34 };
            btnClose.Click += (s, e) => this.Close();
            pnlButtons.Controls.Add(btnClose);

            btnExportExcel = new Button { Text = "Xuất Excel...", Width = 100, Height = 34 };
            btnExportExcel.Click += BtnExportExcel_Click;
            pnlButtons.Controls.Add(btnExportExcel);

            btnDrawMarks = new Button { Text = "Đánh dấu lỗi CAD", Width = 120, Height = 34 };
            btnDrawMarks.Click += BtnDrawMarks_Click;
            pnlButtons.Controls.Add(btnDrawMarks);

            btnDrawRays = new Button { Text = "Vẽ tia ngắm CAD", Width = 120, Height = 34 };
            btnDrawRays.Click += BtnDrawRays_Click;
            pnlButtons.Controls.Add(btnDrawRays);

            btnZoomCAD = new Button { Text = "Zoom CAD", Width = 85, Height = 34 };
            btnZoomCAD.Click += BtnZoomCAD_Click;
            pnlButtons.Controls.Add(btnZoomCAD);

            btnAnalyze = new Button
            {
                Text = "Phân Tích & Kiểm Tra",
                Width = 165,
                Height = 34,
                BackColor = WinFormsColor.FromArgb(28, 64, 112),
                ForeColor = WinFormsColor.White,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnAnalyze.Click += BtnAnalyze_Click;
            pnlButtons.Controls.Add(btnAnalyze);

            lblSummary = new WinFormsLabel
            {
                Text = "Chưa phân tích.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = WinFormsColor.DarkBlue
            };
            pnlBottom.Controls.Add(lblSummary);
        }

        /// <summary>Builds and returns the existing three-column container.</summary>
        private TableLayoutPanel BuildTopContainer()
        {
            // 3. Panel Top Controls (Dock: Top, Chiều cao 312px thoáng đãng)
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 312,
                Padding = new Padding(12, 10, 12, 4)
            };
            this.Controls.Add(pnlTop);

            var topTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23f));
            pnlTop.Controls.Add(topTable);
            return topTable;
        }

        /// <summary>Builds the existing results grid.</summary>
        private void BuildResultsGroup()
        {
            // ==========================================
            // 4. BẢNG KẾT QUẢ DATAGRIDVIEW (DOCK FILL Ở GIỮA)
            // ==========================================
            var grpResults = new GroupBox
            {
                Text = "4. Bảng Kết Quả Phân Tích Chi Tiết (Double-click dòng để Zoom đến vị trí trên CAD)",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            this.Controls.Add(grpResults);

            var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular) };
            grpResults.Controls.Add(pnlGrid);

            dgvResults = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = WinFormsColor.White,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            dgvResults.DoubleClick += DgvResults_DoubleClick;
            pnlGrid.Controls.Add(dgvResults);

            SetupGridColumns();
        }

        private void SetupGridColumns()
        {
            dgvResults.Columns.Clear();
            dgvResults.Columns.Add("colStt", "STT");
            dgvResults.Columns.Add("colStatus", "Trạng thái");
            dgvResults.Columns.Add("colStation", "Lý trình");
            dgvResults.Columns.Add("colGrade", "Độ dốc i (%)");
            dgvResults.Columns.Add("colRadius", "Bán kính R (m)");
            dgvResults.Columns.Add("colReqSight", "Tầm nhìn yc (m)");
            dgvResults.Columns.Add("colActSight", "Khả kiến (m)");
            dgvResults.Columns.Add("colClearance", "Dạt bụng M (m)");
            dgvResults.Columns.Add("colDetail", "Chi tiết đánh giá");

            dgvResults.Columns["colStt"].Width = 45;
            dgvResults.Columns["colStatus"].Width = 95;
            dgvResults.Columns["colStation"].Width = 105;
            dgvResults.Columns["colGrade"].Width = 95;
            dgvResults.Columns["colRadius"].Width = 110;
            dgvResults.Columns["colReqSight"].Width = 115;
            dgvResults.Columns["colActSight"].Width = 105;
            dgvResults.Columns["colClearance"].Width = 105;
            dgvResults.Columns["colDetail"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            foreach (DataGridViewColumn col in dgvResults.Columns)
            {
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }
    }
}

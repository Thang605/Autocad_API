// (C) Copyright 2026 by T27
// Giao diện Form điều khiển & giám sát AutoCAD / Civil 3D MCP Bridge Server (AT_Khoi_Dong_Mcp_Bridge)
//
using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;

namespace MyFirstProject.Acad_Tool
{
    public class KhoiDongMcpBridgeForm : Form
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PERSISTENT STATE (Ghi nhớ thông số lần chạy trước)
        // ═══════════════════════════════════════════════════════════════════════
        private static int _lastPort = 18888;
        private static bool _lastAutoStartOnLoad = true;
        private static bool _lastAutoZoomExtents = true;
        private static bool _lastIncludeCivilInStatus = true;
        private static Size _lastFormSize = new Size(560, 460);

        public static int SavedPort => _lastPort;
        public static bool SavedAutoStart => _lastAutoStartOnLoad;
        public static bool SavedAutoZoom => _lastAutoZoomExtents;
        public static bool SavedIncludeCivilInStatus => _lastIncludeCivilInStatus;

        // UI Controls
        private NumericUpDown _numPort = null!;
        private WinFormsCheckBox _chkAutoStart = null!;
        private WinFormsCheckBox _chkAutoZoom = null!;
        private WinFormsCheckBox _chkIncludeCivil = null!;
        private WinFormsLabel _lblStatusValue = null!;
        private WinFormsTextBox _txtEndpoint = null!;
        private WinFormsTextBox _txtCivilSummary = null!;
        private WinFormsButton _btnScanCivil = null!;
        private WinFormsButton _btnStartStop = null!;
        private WinFormsButton _btnClose = null!;

        public KhoiDongMcpBridgeForm()
        {
            InitializeComponent();
            RestoreLastSettings();
            RefreshBridgeStatusUI();
        }

        private void InitializeComponent()
        {
            this.Text = "AT_Khoi_Dong_Mcp_Bridge - Cầu nối AI Antigravity MCP & Civil 3D";
            this.Font = new System.Drawing.Font("Segoe UI", 9F, FontStyle.Regular);
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(520, 430);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 4
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            // 1. GroupBox Trạng thái
            var grpStatus = new GroupBox
            {
                Text = "Trạng thái kết nối MCP Server (Antigravity 2.0 - Bridge v3.0 Civil 3D)",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true
            };

            var statusGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true
            };
            statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            statusGrid.Controls.Add(new WinFormsLabel { Text = "Trạng thái Bridge:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _lblStatusValue = new WinFormsLabel
            {
                Text = "Đang kiểm tra...",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Left
            };
            statusGrid.Controls.Add(_lblStatusValue, 1, 0);

            statusGrid.Controls.Add(new WinFormsLabel { Text = "Địa chỉ HTTP:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _txtEndpoint = new WinFormsTextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Text = "http://127.0.0.1:18888/ (Hỗ trợ /status, /civil, /draw, /inspect, /lisp)"
            };
            statusGrid.Controls.Add(_txtEndpoint, 1, 1);
            grpStatus.Controls.Add(statusGrid);

            // 2. GroupBox Cấu hình
            var grpConfig = new GroupBox
            {
                Text = "Cấu hình thông số MCP Bridge",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true
            };

            var cfgGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true
            };
            cfgGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            cfgGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cfgGrid.Controls.Add(new WinFormsLabel { Text = "Cổng lắng nghe (Port):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _numPort = new NumericUpDown
            {
                Minimum = 1024,
                Maximum = 65535,
                Value = 18888,
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            _numPort.ValueChanged += (s, e) =>
            {
                _txtEndpoint.Text = $"http://127.0.0.1:{(int)_numPort.Value}/ (Endpoints: /status, /civil, /draw, /inspect)";
            };
            cfgGrid.Controls.Add(_numPort, 1, 0);

            _chkAutoStart = new WinFormsCheckBox
            {
                Text = "Tự động bật MCP Bridge ngay khi nạp DLL vào AutoCAD / Civil 3D",
                AutoSize = true,
                Checked = true
            };
            cfgGrid.SetColumnSpan(_chkAutoStart, 2);
            cfgGrid.Controls.Add(_chkAutoStart, 0, 1);

            _chkAutoZoom = new WinFormsCheckBox
            {
                Text = "Tự động Zoom Extents sau khi nhận lệnh vẽ từ AI",
                AutoSize = true,
                Checked = true
            };
            cfgGrid.SetColumnSpan(_chkAutoZoom, 2);
            cfgGrid.Controls.Add(_chkAutoZoom, 0, 2);

            _chkIncludeCivil = new WinFormsCheckBox
            {
                Text = "Tự động đính kèm thống kê đối tượng Civil 3D trong phản hồi /status",
                AutoSize = true,
                Checked = true
            };
            cfgGrid.SetColumnSpan(_chkIncludeCivil, 2);
            cfgGrid.Controls.Add(_chkIncludeCivil, 0, 3);

            grpConfig.Controls.Add(cfgGrid);

            // 3. GroupBox Dữ liệu Civil 3D trên bản vẽ hiện hành
            var grpCivil = new GroupBox
            {
                Text = "Dữ liệu Civil 3D trên bản vẽ hiện hành (/civil endpoint)",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            _txtCivilSummary = new WinFormsTextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
                Font = new System.Drawing.Font("Consolas", 9F, FontStyle.Regular)
            };
            grpCivil.Controls.Add(_txtCivilSummary);

            // 4. Thanh nút điều khiển
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Width = 95,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };
            _btnClose.Click += (s, e) =>
            {
                SaveCurrentSettings();
                this.Close();
            };

            _btnStartStop = new WinFormsButton
            {
                Text = "Khởi động / Làm mới Bridge",
                Width = 185,
                Height = 32,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnStartStop.Click += BtnStartStop_Click;

            _btnScanCivil = new WinFormsButton
            {
                Text = "Quét dữ liệu Civil 3D",
                Width = 155,
                Height = 32
            };
            _btnScanCivil.Click += (s, e) =>
            {
                _txtCivilSummary.Text = Civil3DCsharp.AT_McpBridgeServer.GetQuickCivilSummaryText();
            };

            pnlButtons.Controls.Add(_btnClose);
            pnlButtons.Controls.Add(_btnStartStop);
            pnlButtons.Controls.Add(_btnScanCivil);

            mainLayout.Controls.Add(grpStatus, 0, 0);
            mainLayout.Controls.Add(grpConfig, 0, 1);
            mainLayout.Controls.Add(grpCivil, 0, 2);
            mainLayout.Controls.Add(pnlButtons, 0, 3);

            this.Controls.Add(mainLayout);
            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void BtnStartStop_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (Civil3DCsharp.AT_McpBridgeServer.IsRunning)
            {
                Civil3DCsharp.AT_McpBridgeServer.StopBridge();
                Civil3DCsharp.AT_McpBridgeServer.EnsureStarted(_lastPort);
            }
            else
            {
                Civil3DCsharp.AT_McpBridgeServer.EnsureStarted(_lastPort);
            }
            RefreshBridgeStatusUI();
        }

        private void RefreshBridgeStatusUI()
        {
            bool running = Civil3DCsharp.AT_McpBridgeServer.IsRunning;
            _lblStatusValue.Text = running
                ? $"ĐANG HOẠT ĐỘNG (Port {Civil3DCsharp.AT_McpBridgeServer.ActivePort} - v3.0 Civil 3D)"
                : "ĐANG DỪNG";
            _lblStatusValue.ForeColor = running ? Color.FromArgb(16, 137, 62) : Color.FromArgb(196, 43, 28);
            _txtEndpoint.Text = $"http://127.0.0.1:{(int)_numPort.Value}/ (Endpoints: /status, /civil, /draw, /inspect)";
            _btnStartStop.Text = running ? "Khởi động lại Bridge" : "Bật MCP Bridge";
            _txtCivilSummary.Text = Civil3DCsharp.AT_McpBridgeServer.GetQuickCivilSummaryText();
        }

        public void SaveCurrentSettings()
        {
            _lastPort = (int)_numPort.Value;
            _lastAutoStartOnLoad = _chkAutoStart.Checked;
            _lastAutoZoomExtents = _chkAutoZoom.Checked;
            _lastIncludeCivilInStatus = _chkIncludeCivil.Checked;
            if (this.WindowState == FormWindowState.Normal)
            {
                _lastFormSize = this.Size;
            }
        }

        public void RestoreLastSettings()
        {
            _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, _lastPort));
            _chkAutoStart.Checked = _lastAutoStartOnLoad;
            _chkAutoZoom.Checked = _lastAutoZoomExtents;
            _chkIncludeCivil.Checked = _lastIncludeCivilInStatus;
            _txtEndpoint.Text = $"http://127.0.0.1:{_lastPort}/ (Endpoints: /status, /civil, /draw, /inspect)";
            if (_lastFormSize.Width >= this.MinimumSize.Width && _lastFormSize.Height >= this.MinimumSize.Height)
            {
                this.Size = _lastFormSize;
            }
        }
    }
}

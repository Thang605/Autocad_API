using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace CAD_Reloader
{
    public class NetReloadForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT SETTINGS (Ghi nhớ cấu hình giữa các lần gọi)
        // ═══════════════════════════════════════════════════════════════
        public static string LastDllPath { get; set; } = @"C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\Civil3D_Tools.dll";
        public static bool LastCopyPdb { get; set; } = true;
        public static bool LastListCommands { get; set; } = true;
        public static bool LastAutoCloseOnSuccess { get; set; } = false;
        private static Size _lastFormSize = new Size(650, 520);

        // ═══════════════════════════════════════════════════════════════
        //  CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private TextBox txtDllPath = null!;
        private Button btnBrowse = null!;
        private WinFormsLabel lblFileInfo = null!;

        private CheckBox chkCopyPdb = null!;
        private CheckBox chkListCommands = null!;
        private CheckBox chkAutoClose = null!;

        private TextBox txtLog = null!;
        private Button btnReload = null!;
        private Button btnClose = null!;

        public NetReloadForm()
        {
            InitializeComponent();
            RestoreLastSettings();
            UpdateFileInfo();
        }

        private void InitializeComponent()
        {
            this.Text = "NETRELOAD — Tải Lại Assembly Không Khóa File DLL";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(580, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = false;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            // Tiêu đề
            var lblHeader = new WinFormsLabel
            {
                Text = "⚡ AUTOCAD / CIVIL 3D .NET FAST RELOADER",
                Location = new Point(16, 14),
                Size = new Size(600, 26),
                Font = new WinFormsFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Group 1: Tệp DLL đích
            var grpDll = new GroupBox
            {
                Text = "Tệp DLL nguồn (Target Assembly)",
                Location = new Point(16, 46),
                Size = new Size(602, 105),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblPath = new WinFormsLabel
            {
                Text = "Đường dẫn file .dll:",
                Location = new Point(14, 25),
                Size = new Size(120, 20)
            };

            txtDllPath = new TextBox
            {
                Location = new Point(135, 22),
                Size = new Size(375, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtDllPath.TextChanged += (s, e) => UpdateFileInfo();

            btnBrowse = new Button
            {
                Text = "📂 Chọn file...",
                Location = new Point(516, 20),
                Size = new Size(74, 27),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowse.Click += BtnBrowse_Click;

            lblFileInfo = new WinFormsLabel
            {
                Text = "Trạng thái file: Đang kiểm tra...",
                Location = new Point(14, 55),
                Size = new Size(574, 40),
                ForeColor = Color.DarkSlateGray,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpDll.Controls.AddRange(new Control[] { lblPath, txtDllPath, btnBrowse, lblFileInfo });

            // Group 2: Tùy chọn tải lại
            var grpOpts = new GroupBox
            {
                Text = "Tùy chọn tải lại",
                Location = new Point(16, 158),
                Size = new Size(602, 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkCopyPdb = new CheckBox
            {
                Text = "Sao chép kèm file .pdb (hỗ trợ Debug)",
                Location = new Point(16, 26),
                Size = new Size(230, 24),
                Checked = true
            };

            chkListCommands = new CheckBox
            {
                Text = "Liệt kê danh sách lệnh phát hiện",
                Location = new Point(255, 26),
                Size = new Size(210, 24),
                Checked = true
            };

            chkAutoClose = new CheckBox
            {
                Text = "Đóng Form sau khi nạp",
                Location = new Point(475, 26),
                Size = new Size(120, 24),
                Checked = false
            };

            grpOpts.Controls.AddRange(new Control[] { chkCopyPdb, chkListCommands, chkAutoClose });

            // Group 3: Nhật ký nạp (Log)
            var grpLog = new GroupBox
            {
                Text = "Nhật ký nạp (Execution Log)",
                Location = new Point(16, 230),
                Size = new Size(602, 195),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(33, 37, 41),
                Font = new WinFormsFont("Consolas", 9F, FontStyle.Regular),
                Location = new Point(14, 22),
                Size = new Size(574, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            grpLog.Controls.Add(txtLog);

            // Nút hành động phía dưới
            btnReload = new Button
            {
                Text = "⚡ Tải lại ngay (Reload Now)",
                Location = new Point(366, 436),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnReload.Click += BtnReload_Click;

            btnClose = new Button
            {
                Text = "Đóng",
                Location = new Point(534, 436),
                Size = new Size(84, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblHeader, grpDll, grpOpts, grpLog, btnReload, btnClose
            });

            this.AcceptButton = btnReload;
            this.CancelButton = btnClose;
            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn file .NET Assembly (.dll) cần nạp vào AutoCAD";
                ofd.Filter = "AutoCAD .NET Assembly (*.dll)|*.dll|All files (*.*)|*.*";
                if (File.Exists(txtDllPath.Text))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(txtDllPath.Text);
                    ofd.FileName = Path.GetFileName(txtDllPath.Text);
                }

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    txtDllPath.Text = ofd.FileName;
                    UpdateFileInfo();
                }
            }
        }

        private void UpdateFileInfo()
        {
            string path = txtDllPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                lblFileInfo.Text = "⚠ Chưa nhập đường dẫn tệp DLL.";
                lblFileInfo.ForeColor = Color.Red;
                btnReload.Enabled = false;
                return;
            }

            if (!File.Exists(path))
            {
                lblFileInfo.Text = $"❌ Không tìm thấy file: {path}";
                lblFileInfo.ForeColor = Color.Red;
                btnReload.Enabled = false;
                return;
            }

            try
            {
                var fi = new FileInfo(path);
                lblFileInfo.Text = $"✓ Tồn tại: {fi.Length / 1024.0:F1} KB | Cập nhật lần cuối: {fi.LastWriteTime:dd/MM/yyyy HH:mm:ss}";
                lblFileInfo.ForeColor = Color.DarkGreen;
                btnReload.Enabled = true;
            }
            catch (System.Exception ex)
            {
                lblFileInfo.Text = $"⚠ Lỗi đọc file: {ex.Message}";
                lblFileInfo.ForeColor = Color.Red;
                btnReload.Enabled = false;
            }
        }

        private void BtnReload_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            string dllPath = txtDllPath.Text.Trim();

            AppendLog($"[{DateTime.Now:HH:mm:ss}] Bắt đầu quy trình nạp lại...");

            bool ok = ReloaderEngine.ReloadAssembly(
                dllPath,
                chkCopyPdb.Checked,
                chkListCommands.Checked,
                msg => AppendLog(msg)
            );

            if (ok)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✅ TẢI LẠI THÀNH CÔNG!");
                if (chkAutoClose.Checked)
                {
                    this.Close();
                }
            }
            else
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ❌ TẢI LẠI THẤT BẠI. Vui lòng kiểm tra lại log bên trên.");
            }
        }

        private void AppendLog(string message)
        {
            txtLog.AppendText(message + Environment.NewLine);
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        public void SaveCurrentSettings()
        {
            LastDllPath = txtDllPath.Text.Trim();
            LastCopyPdb = chkCopyPdb.Checked;
            LastListCommands = chkListCommands.Checked;
            LastAutoCloseOnSuccess = chkAutoClose.Checked;
            _lastFormSize = this.Size;
        }

        public void RestoreLastSettings()
        {
            txtDllPath.Text = LastDllPath;
            chkCopyPdb.Checked = LastCopyPdb;
            chkListCommands.Checked = LastListCommands;
            chkAutoClose.Checked = LastAutoCloseOnSuccess;
        }
    }
}

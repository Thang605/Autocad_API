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
        public static string LastDllPath { get; set; } = string.Empty;
        public static string CustomRepoPath { get; set; } = string.Empty;
        public static bool AutoBuildOnFastReload { get; set; } = true;
        public static bool LastCopyPdb { get; set; } = true;
        public static bool LastListCommands { get; set; } = true;
        public static bool LastAutoCloseOnSuccess { get; set; } = false;
        private static Size _lastFormSize = new Size(700, 620);

        // ═══════════════════════════════════════════════════════════════
        //  CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private TextBox txtRepoPath = null!;
        private Button btnBrowseRepo = null!;
        private Button btnAutoDetectRepo = null!;

        private TextBox txtDllPath = null!;
        private Button btnBrowse = null!;
        private Button btnScanLatest = null!;
        private WinFormsLabel lblFileInfo = null!;

        private CheckBox chkAutoBuild = null!;
        private CheckBox chkCopyPdb = null!;
        private CheckBox chkListCommands = null!;
        private CheckBox chkAutoClose = null!;

        private TextBox txtLog = null!;
        private Button btnBuildAndReload = null!;
        private Button btnReloadOnly = null!;
        private Button btnClose = null!;

        public NetReloadForm()
        {
            InitializeComponent();
            RestoreLastSettings();
            UpdateFileInfo();
        }

        private void InitializeComponent()
        {
            this.Text = "NETRELOAD — Tự Động Biên Dịch & Nạp Lại Assembly (AutoCAD / Civil 3D)";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(660, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = false;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            // Tiêu đề
            var lblHeader = new WinFormsLabel
            {
                Text = "⚡ AUTOCAD / CIVIL 3D FAST RELOADER (.NET 10)",
                Location = new Point(16, 12),
                Size = new Size(650, 24),
                Font = new WinFormsFont("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Group 0: Thư mục nguồn Repo (Tương thích mọi máy tính / vị trí Dropbox)
            var grpRepo = new GroupBox
            {
                Text = "Thư mục mã nguồn dự án (Project Repo Root)",
                Location = new Point(16, 38),
                Size = new Size(652, 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblRepo = new WinFormsLabel
            {
                Text = "Thư mục Repo:",
                Location = new Point(12, 25),
                Size = new Size(100, 20)
            };

            txtRepoPath = new TextBox
            {
                Location = new Point(115, 22),
                Size = new Size(390, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnAutoDetectRepo = new Button
            {
                Text = "🔍 Tự dò tìm",
                Location = new Point(512, 20),
                Size = new Size(80, 27),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnAutoDetectRepo.Click += (s, e) =>
            {
                CustomRepoPath = string.Empty;
                string detected = ProjectBuildEngine.ResolveRepoRootDir();
                txtRepoPath.Text = detected;
                CustomRepoPath = detected;
                AppendLog($"✓ Tự động nhận diện thư mục Repo: {detected}");
            };

            btnBrowseRepo = new Button
            {
                Text = "📂 Chọn...",
                Location = new Point(596, 20),
                Size = new Size(46, 27),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowseRepo.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                fbd.Description = "Chọn thư mục gốc của Repo chứa MyFirstProject";
                if (Directory.Exists(txtRepoPath.Text)) fbd.SelectedPath = txtRepoPath.Text;
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtRepoPath.Text = fbd.SelectedPath;
                    CustomRepoPath = fbd.SelectedPath;
                    AppendLog($"✓ Đã thiết lập thư mục Repo: {fbd.SelectedPath}");
                }
            };

            grpRepo.Controls.AddRange(new Control[] { lblRepo, txtRepoPath, btnAutoDetectRepo, btnBrowseRepo });

            // Group 1: Tệp DLL đích
            var grpDll = new GroupBox
            {
                Text = "Tệp DLL nguồn (Target Assembly)",
                Location = new Point(16, 110),
                Size = new Size(652, 105),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblPath = new WinFormsLabel
            {
                Text = "Đường dẫn file .dll:",
                Location = new Point(12, 25),
                Size = new Size(115, 20)
            };

            txtDllPath = new TextBox
            {
                Location = new Point(130, 22),
                Size = new Size(355, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtDllPath.TextChanged += (s, e) => UpdateFileInfo();

            btnScanLatest = new Button
            {
                Text = "🔄 Bản mới nhất",
                Location = new Point(492, 20),
                Size = new Size(100, 27),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnScanLatest.Click += (s, e) =>
            {
                string latest = NetReloadCommands.ResolveLatestTargetDll();
                if (!string.IsNullOrEmpty(latest))
                {
                    txtDllPath.Text = latest;
                    UpdateFileInfo();
                    AppendLog($"✓ Đã quét tìm thấy DLL mới nhất: {latest}");
                }
            };

            btnBrowse = new Button
            {
                Text = "📂 Chọn...",
                Location = new Point(596, 20),
                Size = new Size(46, 27),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowse.Click += BtnBrowse_Click;

            lblFileInfo = new WinFormsLabel
            {
                Text = "Trạng thái file: Đang kiểm tra...",
                Location = new Point(12, 55),
                Size = new Size(630, 42),
                ForeColor = Color.DarkSlateGray,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpDll.Controls.AddRange(new Control[] { lblPath, txtDllPath, btnScanLatest, btnBrowse, lblFileInfo });

            // Group 2: Tùy chọn tải lại
            var grpOpts = new GroupBox
            {
                Text = "Tùy chọn tải lại & Biên dịch",
                Location = new Point(16, 222),
                Size = new Size(652, 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkAutoBuild = new CheckBox
            {
                Text = "Tự động Build khi gõ RELOAD",
                Location = new Point(12, 25),
                Size = new Size(200, 24),
                Checked = true
            };

            chkCopyPdb = new CheckBox
            {
                Text = "Sao chép .pdb",
                Location = new Point(220, 25),
                Size = new Size(110, 24),
                Checked = true
            };

            chkListCommands = new CheckBox
            {
                Text = "Liệt kê lệnh",
                Location = new Point(340, 25),
                Size = new Size(110, 24),
                Checked = true
            };

            chkAutoClose = new CheckBox
            {
                Text = "Đóng khi xong",
                Location = new Point(460, 25),
                Size = new Size(120, 24),
                Checked = false
            };

            grpOpts.Controls.AddRange(new Control[] { chkAutoBuild, chkCopyPdb, chkListCommands, chkAutoClose });

            // Group 3: Nhật ký nạp (Log)
            var grpLog = new GroupBox
            {
                Text = "Nhật ký nạp (Execution Log)",
                Location = new Point(16, 294),
                Size = new Size(652, 225),
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
                Location = new Point(12, 22),
                Size = new Size(628, 190),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            grpLog.Controls.Add(txtLog);

            // Nút hành động phía dưới
            btnBuildAndReload = new Button
            {
                Text = "🔨 Biên dịch & Nạp lại (Build & Reload)",
                Location = new Point(242, 528),
                Size = new Size(230, 38),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnBuildAndReload.Click += BtnBuildAndReload_Click;

            btnReloadOnly = new Button
            {
                Text = "⚡ Nạp file đã chọn",
                Location = new Point(478, 528),
                Size = new Size(120, 38),
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnReloadOnly.Click += BtnReloadOnly_Click;

            btnClose = new Button
            {
                Text = "Đóng",
                Location = new Point(604, 528),
                Size = new Size(64, 38),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblHeader, grpRepo, grpDll, grpOpts, grpLog, btnBuildAndReload, btnReloadOnly, btnClose
            });

            this.AcceptButton = btnBuildAndReload;
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
                lblFileInfo.Text = "⚠ Chưa nhập đường dẫn tệp DLL (Sẽ tự động tìm bản mới nhất khi build).";
                lblFileInfo.ForeColor = Color.DarkOrange;
                btnReloadOnly.Enabled = false;
                return;
            }

            if (!File.Exists(path))
            {
                lblFileInfo.Text = $"❌ Không tìm thấy file: {path}";
                lblFileInfo.ForeColor = Color.Red;
                btnReloadOnly.Enabled = false;
                return;
            }

            try
            {
                var fi = new FileInfo(path);
                lblFileInfo.Text = $"✓ Tồn tại: {fi.Name}\n  Dung lượng: {fi.Length / 1024.0:F1} KB | Sửa lần cuối: {fi.LastWriteTime:dd/MM/yyyy HH:mm:ss}";
                lblFileInfo.ForeColor = Color.DarkGreen;
                btnReloadOnly.Enabled = true;
            }
            catch (System.Exception ex)
            {
                lblFileInfo.Text = $"⚠ Lỗi đọc file: {ex.Message}";
                lblFileInfo.ForeColor = Color.Red;
                btnReloadOnly.Enabled = false;
            }
        }

        private void BtnBuildAndReload_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            AppendLog($"\n═══════════════════════════════════════════════════════");
            AppendLog($"[{DateTime.Now:HH:mm:ss}] 🔨 BẮT ĐẦU BIÊN DỊCH VÀ NẠP LẠI (BUILD & RELOAD)...");

            btnBuildAndReload.Enabled = false;
            btnReloadOnly.Enabled = false;

            try
            {
                bool buildOk = ProjectBuildEngine.BuildProject(
                    msg => AppendLog(msg),
                    out string newDllPath
                );

                if (buildOk && !string.IsNullOrEmpty(newDllPath) && File.Exists(newDllPath))
                {
                    txtDllPath.Text = newDllPath;
                    UpdateFileInfo();

                    AppendLog($"[{DateTime.Now:HH:mm:ss}] 🔄 Đang nạp Assembly mới vào AutoCAD...");

                    bool reloadOk = ReloaderEngine.ReloadAssembly(
                        newDllPath,
                        chkCopyPdb.Checked,
                        chkListCommands.Checked,
                        msg => AppendLog(msg)
                    );

                    if (reloadOk)
                    {
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] ✅ TẢI LẠI THÀNH CÔNG! Code mới đã có hiệu lực.");
                        if (chkAutoClose.Checked)
                        {
                            this.Close();
                        }
                    }
                    else
                    {
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] ❌ NẠP ASSEMBLY THẤT BẠI.");
                    }
                }
                else
                {
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] ❌ BIÊN DỊCH THẤT BẠI. Vui lòng kiểm tra lại log bên trên.");
                }
            }
            finally
            {
                btnBuildAndReload.Enabled = true;
                btnReloadOnly.Enabled = true;
            }
        }

        private void BtnReloadOnly_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            string dllPath = txtDllPath.Text.Trim();

            AppendLog($"\n═══════════════════════════════════════════════════════");
            AppendLog($"[{DateTime.Now:HH:mm:ss}] ⚡ Nạp trực tiếp file đã chọn: {Path.GetFileName(dllPath)}...");

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
            CustomRepoPath = txtRepoPath.Text.Trim();
            LastDllPath = txtDllPath.Text.Trim();
            AutoBuildOnFastReload = chkAutoBuild.Checked;
            LastCopyPdb = chkCopyPdb.Checked;
            LastListCommands = chkListCommands.Checked;
            LastAutoCloseOnSuccess = chkAutoClose.Checked;
            _lastFormSize = this.Size;
        }

        public void RestoreLastSettings()
        {
            if (string.IsNullOrEmpty(CustomRepoPath))
            {
                CustomRepoPath = ProjectBuildEngine.ResolveRepoRootDir();
            }
            txtRepoPath.Text = CustomRepoPath;

            if (string.IsNullOrEmpty(LastDllPath))
            {
                LastDllPath = NetReloadCommands.ResolveLatestTargetDll();
            }
            txtDllPath.Text = LastDllPath;

            chkAutoBuild.Checked = AutoBuildOnFastReload;
            chkCopyPdb.Checked = LastCopyPdb;
            chkListCommands.Checked = LastListCommands;
            chkAutoClose.Checked = LastAutoCloseOnSuccess;
        }
    }
}

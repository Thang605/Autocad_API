using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Form cho lệnh điều chỉnh Profile theo Polyline
    /// </summary>
    public class AdjustProfileForm : Form
    {
        // Properties trả về kết quả
        public ObjectId SelectedProfileId { get; private set; } = ObjectId.Null;
        public int AdjustOption { get; private set; } = 1;
        public bool FormAccepted { get; private set; } = false;

        // UI Controls
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblProfileView = null!;
        private WinFormsLabel lblAlignment = null!;
        private TextBox txtProfileView = null!;
        private TextBox txtAlignment = null!;

        private GroupBox grpProfiles = null!;
        private ListBox lstProfiles = null!;

        private GroupBox grpOptions = null!;
        private RadioButton radReplaceAll = null!;
        private RadioButton radReplaceInRange = null!;
        private RadioButton radAddNew = null!;

        private Button btnPick = null!;

        private Button btnOK = null!;
        private Button btnCancel = null!;

        // Data
        private string _profileViewName;
        private string _alignmentName;
        private List<(ObjectId id, string name, ProfileType type)> _profileList;

        public AdjustProfileForm(string profileViewName, string alignmentName,
                                  List<(ObjectId id, string name, ProfileType type)> profileList)
        {
            _profileViewName = profileViewName;
            _alignmentName = alignmentName;
            _profileList = profileList;
            InitializeComponent();
            LoadProfiles();
        }

        private void InitializeComponent()
        {
            // Standard Font
            var standardFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Regular);
            var boldFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Bold);
            var titleFont = new WinFormsFont("Segoe UI", 14F, FontStyle.Bold);

            // Initialize controls
            this.lblTitle = new WinFormsLabel();
            this.lblProfileView = new WinFormsLabel();
            this.lblAlignment = new WinFormsLabel();
            this.txtProfileView = new TextBox();
            this.txtAlignment = new TextBox();

            this.grpProfiles = new GroupBox();
            this.lstProfiles = new ListBox();

            this.grpOptions = new GroupBox();
            this.radReplaceAll = new RadioButton();
            this.radReplaceInRange = new RadioButton();
            this.radAddNew = new RadioButton();

            this.btnPick = new Button();

            this.btnOK = new Button();
            this.btnCancel = new Button();

            this.SuspendLayout();

            // Form
            this.Text = "Điều chỉnh Profile theo Polyline";
            this.Size = new Size(500, 530);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = standardFont;

            // Title Label
            this.lblTitle.Text = "ĐIỀU CHỈNH PROFILE THEO POLYLINE";
            this.lblTitle.Font = titleFont;
            this.lblTitle.Location = new WinFormsPoint(20, 15);
            this.lblTitle.Size = new Size(440, 30);
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTitle.ForeColor = Color.FromArgb(0, 102, 204);

            // ProfileView Info
            this.lblProfileView.Text = "Profile View:";
            this.lblProfileView.Font = boldFont;
            this.lblProfileView.Location = new WinFormsPoint(20, 55);
            this.lblProfileView.Size = new Size(100, 25);

            this.txtProfileView.Location = new WinFormsPoint(125, 53);
            this.txtProfileView.Size = new Size(340, 25);
            this.txtProfileView.ReadOnly = true;
            this.txtProfileView.BackColor = Color.WhiteSmoke;
            this.txtProfileView.Text = _profileViewName;
            this.txtProfileView.Font = standardFont;

            // Alignment Info
            this.lblAlignment.Text = "Alignment:";
            this.lblAlignment.Font = boldFont;
            this.lblAlignment.Location = new WinFormsPoint(20, 85);
            this.lblAlignment.Size = new Size(100, 25);

            this.txtAlignment.Location = new WinFormsPoint(125, 83);
            this.txtAlignment.Size = new Size(340, 25);
            this.txtAlignment.ReadOnly = true;
            this.txtAlignment.BackColor = Color.WhiteSmoke;
            this.txtAlignment.Text = _alignmentName;
            this.txtAlignment.Font = standardFont;

            // Profiles Group
            this.grpProfiles.Text = "Chọn Profile cần điều chỉnh";
            this.grpProfiles.Font = boldFont;
            this.grpProfiles.Location = new WinFormsPoint(20, 120);
            this.grpProfiles.Size = new Size(445, 190);
            this.grpProfiles.ForeColor = Color.Black;

            // Profiles ListBox
            this.lstProfiles.Location = new WinFormsPoint(15, 25);
            this.lstProfiles.Size = new Size(415, 110);
            this.lstProfiles.Font = standardFont;
            this.lstProfiles.SelectionMode = System.Windows.Forms.SelectionMode.One;

            this.grpProfiles.Controls.Add(lstProfiles);

            // Pick Button
            this.btnPick.Text = "🖱️ Chọn trên bản vẽ";
            this.btnPick.Location = new WinFormsPoint(250, 145);
            this.btnPick.Size = new Size(180, 30);
            this.btnPick.Font = standardFont;
            this.btnPick.BackColor = Color.LightGray;
            this.btnPick.Click += BtnPick_Click;

            this.grpProfiles.Controls.Add(btnPick);

            // Options Group
            this.grpOptions.Text = "Tùy chọn điều chỉnh";
            this.grpOptions.Font = boldFont;
            this.grpOptions.Location = new WinFormsPoint(20, 320);
            this.grpOptions.Size = new Size(445, 110);
            this.grpOptions.ForeColor = Color.Black;

            // Radio buttons
            this.radReplaceAll.Text = "Thay thế toàn bộ PVI (xóa hết PVI cũ)";
            this.radReplaceAll.Font = standardFont;
            this.radReplaceAll.Location = new WinFormsPoint(15, 25);
            this.radReplaceAll.Size = new Size(400, 25);
            this.radReplaceAll.Checked = true;

            this.radReplaceInRange.Text = "Thay thế PVI trong phạm vi Polyline (giữ PVI ngoài phạm vi)";
            this.radReplaceInRange.Font = standardFont;
            this.radReplaceInRange.Location = new WinFormsPoint(15, 50);
            this.radReplaceInRange.Size = new Size(420, 25);

            this.radAddNew.Text = "Thêm PVI mới (giữ nguyên PVI cũ)";
            this.radAddNew.Font = standardFont;
            this.radAddNew.Location = new WinFormsPoint(15, 75);
            this.radAddNew.Size = new Size(400, 25);

            this.grpOptions.Controls.AddRange(new Control[] {
                radReplaceAll, radReplaceInRange, radAddNew
            });

            // OK Button
            this.btnOK.Text = "Tiếp tục";
            this.btnOK.Location = new WinFormsPoint(270, 440);
            this.btnOK.Size = new Size(100, 35);
            this.btnOK.Font = boldFont;
            this.btnOK.BackColor = Color.FromArgb(0, 122, 204);
            this.btnOK.ForeColor = Color.White;
            this.btnOK.FlatStyle = FlatStyle.Flat;
            this.btnOK.Click += BtnOK_Click;

            // Cancel Button
            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new WinFormsPoint(380, 440);
            this.btnCancel.Size = new Size(85, 35);
            this.btnCancel.Font = standardFont;
            this.btnCancel.Click += BtnCancel_Click;

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                lblTitle,
                lblProfileView, txtProfileView,
                lblAlignment, txtAlignment,
                grpProfiles,
                grpOptions,
                btnOK, btnCancel
            });

            // Keyboard Accessibility
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private void LoadProfiles()
        {
            lstProfiles.Items.Clear();

            foreach (var (id, name, type) in _profileList)
            {
                string typeStr = type == ProfileType.FG ? "[Layout]" : "[Surface]";
                string displayText = $"{name} {typeStr}";
                lstProfiles.Items.Add(new ProfileItem(id, name, type, displayText));
            }

            // Auto-select first Layout profile
            for (int i = 0; i < lstProfiles.Items.Count; i++)
            {
                if (lstProfiles.Items[i] is ProfileItem item && item.Type == ProfileType.FG)
                {
                    lstProfiles.SelectedIndex = i;
                    break;
                }
            }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (lstProfiles.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn Profile cần điều chỉnh!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedItem = lstProfiles.SelectedItem as ProfileItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Lỗi: Không thể lấy thông tin Profile!", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Kiểm tra loại profile
            if (selectedItem.Type != ProfileType.FG)
            {
                MessageBox.Show("Chỉ có thể điều chỉnh Profile loại Layout!\nProfile Surface không thể chỉnh sửa PVI.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedProfileId = selectedItem.Id;

            // Lấy tùy chọn
            if (radReplaceAll.Checked)
                AdjustOption = 1;
            else if (radReplaceInRange.Checked)
                AdjustOption = 2;
            else if (radAddNew.Checked)
                AdjustOption = 3;

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

        private void BtnPick_Click(object? sender, EventArgs e)
        {
            // Ẩn form để người dùng chọn trên bản vẽ
            // Sử dụng EditorUserInteraction để xử lý việc ẩn/hiện form modal đúng cách
            var editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            
            using (var interaction = editor.StartUserInteraction(this))
            {
                var options = new PromptEntityOptions("\n>> Chọn Profile trên bản vẽ (Profile View): ");
                options.SetRejectMessage("\nĐối tượng không phải là Profile.");
                options.AddAllowedClass(typeof(Profile), true);

                var result = editor.GetEntity(options);

                if (result.Status == PromptStatus.OK)
                {
                    ObjectId pickedId = result.ObjectId;
                    
                    // Tìm trong list
                    bool found = false;
                    for (int i = 0; i < lstProfiles.Items.Count; i++)
                    {
                        if (lstProfiles.Items[i] is ProfileItem item && item.Id == pickedId)
                        {
                            lstProfiles.SelectedIndex = i;
                            found = true;
                            break;
                        }
                    }

                    interaction.End(); // Hiện lại form trước khi hiện thông báo

                    if (!found)
                    {
                         MessageBox.Show("Profile được chọn không thuộc Alignment hiện tại hoặc không có trong danh sách.", 
                            "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    interaction.End(); // Hiện lại form nếu hủy chọn
                }
            }
        }

        /// <summary>
        /// Helper class để lưu thông tin Profile trong ListBox
        /// </summary>
        private class ProfileItem
        {
            public ObjectId Id { get; }
            public string Name { get; }
            public ProfileType Type { get; }
            private string DisplayText { get; }

            public ProfileItem(ObjectId id, string name, ProfileType type, string displayText)
            {
                Id = id;
                Name = name;
                Type = type;
                DisplayText = displayText;
            }

            public override string ToString() => DisplayText;
        }
    }
}

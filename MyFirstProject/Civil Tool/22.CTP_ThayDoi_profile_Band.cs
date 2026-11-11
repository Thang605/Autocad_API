using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WinLabel = System.Windows.Forms.Label;
using WinButton = System.Windows.Forms.Button;
using WinComboBox = System.Windows.Forms.ComboBox;
using WinCheckBox = System.Windows.Forms.CheckBox;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTP_ThayDoi_profile_Band_Commands))]

namespace Civil3DCsharp
{
    public class CTP_ThayDoi_profile_Band_Commands
    {
        [CommandMethod("CTP_Thay_Doi_Profile_Band")]
        public static void CTPThayDoiProfileBand()
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Hiển thị form để người dùng chọn thông tin
                    using (ProfileBandChangeForm form = new ProfileBandChangeForm())
                    {
                        DialogResult result = form.ShowDialog();
                        
                        if (result == DialogResult.OK)
                        {
                            ObjectId profileViewId = form.SelectedProfileViewId;
                            
                            // Thực hiện thay đổi profile trong band
                            ChangeProfileInBand(profileViewId, form.Profile1Settings, form.Profile2Settings, tr);
                            
                            A.Ed.WriteMessage($"\nHoàn thành quá trình thay đổi profile trong bands!");
                        }
                        else
                        {
                            A.Ed.WriteMessage("\nNgười dùng đã hủy thao tác.");
                        }
                    }
                    
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi: {ex.Message}");
                    tr.Abort();
                }
            }
        }
        
        private static void ChangeProfileInBand(ObjectId profileViewId, ProfileChangeSettings profile1Settings, ProfileChangeSettings profile2Settings, Transaction tr)
        {
            ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForWrite) as ProfileView;
            if (profileView == null)
            {
                throw new System.Exception("Không thể truy cập ProfileView.");
            }
            
            A.Ed.WriteMessage($"\n=== BẮT ĐẦU THAY ĐỔI PROFILE ===");
            A.Ed.WriteMessage($"\nProfileView: {profileView.Name}");
            
            if (profile1Settings.ShouldChange)
            {
                A.Ed.WriteMessage($"\nProfile1 - Cũ: {profile1Settings.OldProfileName}, Mới: {profile1Settings.NewProfileName}");
            }
            
            if (profile2Settings.ShouldChange)
            {
                A.Ed.WriteMessage($"\nProfile2 - Cũ: {profile2Settings.OldProfileName}, Mới: {profile2Settings.NewProfileName}");
            }
            
            A.Ed.WriteMessage($"\n");
            
            int successCount = 0;
            int skipCount = 0;
            
            try 
            {
                // Xử lý Bottom Bands
                A.Ed.WriteMessage($"\nKiểm tra Bottom Bands...");
                try
                {
                    ProfileViewBandItemCollection bottomBands = profileView.Bands.GetBottomBandItems();
                    bool bottomChanged = false;
                    
                    for (int i = 0; i < bottomBands.Count; i++)
                    {
                        var bandItem = bottomBands[i];
                        A.Ed.WriteMessage($"\n  - Band {i + 1}: {bandItem.BandType}");
                        
                        // Bỏ qua các band type không hỗ trợ Profile1Id/Profile2Id
                        if (bandItem.BandType.ToString() == "HorizontalGeometry")
                        {
                            A.Ed.WriteMessage($"\n    (Bỏ qua - loại band không hỗ trợ thay đổi profile)");
                            continue;
                        }
                        
                        try
                        {
                            // Kiểm tra và thay đổi Profile1Id
                            if (profile1Settings.ShouldChange)
                            {
                                bool shouldChangeProfile1 = false;
                                string currentName = "";
                                
                                // Kiểm tra profile hiện tại (có thể đã bị xóa)
                                if (bandItem.Profile1Id != ObjectId.Null)
                                {
                                    try
                                    {
                                        Profile? profile1 = tr.GetObject(bandItem.Profile1Id, OpenMode.ForRead) as Profile;
                                        currentName = profile1?.Name ?? "";
                                        shouldChangeProfile1 = string.IsNullOrEmpty(profile1Settings.OldProfileName) || 
                                                              profile1?.Name == profile1Settings.OldProfileName;
                                    }
                                    catch
                                    {
                                        // Profile đã bị xóa (eWasErased) -> cho phép thay đổi
                                        currentName = "(đã bị xóa)";
                                        shouldChangeProfile1 = string.IsNullOrEmpty(profile1Settings.OldProfileName);
                                    }
                                }
                                else
                                {
                                    // Band chưa có Profile1 -> cho phép gán mới nếu OldProfileName rỗng
                                    shouldChangeProfile1 = string.IsNullOrEmpty(profile1Settings.OldProfileName);
                                }
                                
                                if (shouldChangeProfile1)
                                {
                                    bottomBands[i].Profile1Id = profile1Settings.NewProfileId;
                                    A.Ed.WriteMessage($"\n    ✓ Đã thay đổi Profile1 từ '{currentName}' sang '{profile1Settings.NewProfileName}'");
                                    bottomChanged = true;
                                    successCount++;
                                }
                            }
                            
                            // Kiểm tra và thay đổi Profile2Id
                            if (profile2Settings.ShouldChange)
                            {
                                bool shouldChangeProfile2 = false;
                                string currentName = "";
                                
                                if (bandItem.Profile2Id != ObjectId.Null)
                                {
                                    try
                                    {
                                        Profile? profile2 = tr.GetObject(bandItem.Profile2Id, OpenMode.ForRead) as Profile;
                                        currentName = profile2?.Name ?? "";
                                        shouldChangeProfile2 = string.IsNullOrEmpty(profile2Settings.OldProfileName) || 
                                                              profile2?.Name == profile2Settings.OldProfileName;
                                    }
                                    catch
                                    {
                                        // Profile đã bị xóa (eWasErased) -> cho phép thay đổi
                                        currentName = "(đã bị xóa)";
                                        shouldChangeProfile2 = string.IsNullOrEmpty(profile2Settings.OldProfileName);
                                    }
                                }
                                else
                                {
                                    shouldChangeProfile2 = string.IsNullOrEmpty(profile2Settings.OldProfileName);
                                }
                                
                                if (shouldChangeProfile2)
                                {
                                    bottomBands[i].Profile2Id = profile2Settings.NewProfileId;
                                    A.Ed.WriteMessage($"\n    ✓ Đã thay đổi Profile2 từ '{currentName}' sang '{profile2Settings.NewProfileName}'");
                                    bottomChanged = true;
                                    successCount++;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n    ⚠ Band {i + 1}: Không thể thay đổi - {ex.Message}");
                            skipCount++;
                        }
                    }
                    
                    if (bottomChanged)
                    {
                        profileView.Bands.SetBottomBandItems(bottomBands);
                        A.Ed.WriteMessage($"\n  ✓ Đã cập nhật Bottom Bands");
                    }
                    else
                    {
                        A.Ed.WriteMessage($"\n  - Không có Bottom Band nào cần thay đổi");
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n  ✗ Lỗi xử lý Bottom Bands: {ex.Message}");
                }
                
                // Xử lý Top Bands
                A.Ed.WriteMessage($"\nKiểm tra Top Bands...");
                try
                {
                    ProfileViewBandItemCollection topBands = profileView.Bands.GetTopBandItems();
                    bool topChanged = false;
                    
                    for (int i = 0; i < topBands.Count; i++)
                    {
                        var bandItem = topBands[i];
                        A.Ed.WriteMessage($"\n  - Band {i + 1}: {bandItem.BandType}");
                        
                        // Bỏ qua các band type không hỗ trợ Profile1Id/Profile2Id
                        if (bandItem.BandType.ToString() == "HorizontalGeometry")
                        {
                            A.Ed.WriteMessage($"\n    (Bỏ qua - loại band không hỗ trợ thay đổi profile)");
                            continue;
                        }
                        
                        try
                        {
                            // Kiểm tra và thay đổi Profile1Id
                            if (profile1Settings.ShouldChange)
                            {
                                bool shouldChangeProfile1 = false;
                                string currentName = "";
                                
                                if (bandItem.Profile1Id != ObjectId.Null)
                                {
                                    try
                                    {
                                        Profile? profile1 = tr.GetObject(bandItem.Profile1Id, OpenMode.ForRead) as Profile;
                                        currentName = profile1?.Name ?? "";
                                        shouldChangeProfile1 = string.IsNullOrEmpty(profile1Settings.OldProfileName) || 
                                                              profile1?.Name == profile1Settings.OldProfileName;
                                    }
                                    catch
                                    {
                                        // Profile đã bị xóa (eWasErased) -> cho phép thay đổi
                                        currentName = "(đã bị xóa)";
                                        shouldChangeProfile1 = string.IsNullOrEmpty(profile1Settings.OldProfileName);
                                    }
                                }
                                else
                                {
                                    shouldChangeProfile1 = string.IsNullOrEmpty(profile1Settings.OldProfileName);
                                }
                                
                                if (shouldChangeProfile1)
                                {
                                    topBands[i].Profile1Id = profile1Settings.NewProfileId;
                                    A.Ed.WriteMessage($"\n    ✓ Đã thay đổi Profile1 từ '{currentName}' sang '{profile1Settings.NewProfileName}'");
                                    topChanged = true;
                                    successCount++;
                                }
                            }
                            
                            // Kiểm tra và thay đổi Profile2Id
                            if (profile2Settings.ShouldChange)
                            {
                                bool shouldChangeProfile2 = false;
                                string currentName = "";
                                
                                if (bandItem.Profile2Id != ObjectId.Null)
                                {
                                    try
                                    {
                                        Profile? profile2 = tr.GetObject(bandItem.Profile2Id, OpenMode.ForRead) as Profile;
                                        currentName = profile2?.Name ?? "";
                                        shouldChangeProfile2 = string.IsNullOrEmpty(profile2Settings.OldProfileName) || 
                                                              profile2?.Name == profile2Settings.OldProfileName;
                                    }
                                    catch
                                    {
                                        // Profile đã bị xóa (eWasErased) -> cho phép thay đổi
                                        currentName = "(đã bị xóa)";
                                        shouldChangeProfile2 = string.IsNullOrEmpty(profile2Settings.OldProfileName);
                                    }
                                }
                                else
                                {
                                    shouldChangeProfile2 = string.IsNullOrEmpty(profile2Settings.OldProfileName);
                                }
                                
                                if (shouldChangeProfile2)
                                {
                                    topBands[i].Profile2Id = profile2Settings.NewProfileId;
                                    A.Ed.WriteMessage($"\n    ✓ Đã thay đổi Profile2 từ '{currentName}' sang '{profile2Settings.NewProfileName}'");
                                    topChanged = true;
                                    successCount++;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n    ⚠ Band {i + 1}: Không thể thay đổi - {ex.Message}");
                            skipCount++;
                        }
                    }
                    
                    if (topChanged)
                    {
                        profileView.Bands.SetTopBandItems(topBands);
                        A.Ed.WriteMessage($"\n  ✓ Đã cập nhật Top Bands");
                    }
                    else
                    {
                        A.Ed.WriteMessage($"\n  - Không có Top Band nào cần thay đổi");
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n  ✗ Lỗi xử lý Top Bands: {ex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nCảnh báo: {ex.Message}");
            }
            
            A.Ed.WriteMessage($"\n=== KẾT QUẢ ===");
            A.Ed.WriteMessage($"\nSố profile reference đã thay đổi thành công: {successCount}");
            A.Ed.WriteMessage($"\nSố band bị bỏ qua (không thể thay đổi): {skipCount}");
            
            if (successCount > 0)
            {
                A.Ed.WriteMessage($"\n✓ Đã thay đổi profile cho {successCount} reference(s)");
            }
            else
            {
                A.Ed.WriteMessage($"\n⚠ Không có profile reference nào được cập nhật.");
            }
        }
    }
    
    public class ProfileChangeSettings
    {
        public bool ShouldChange { get; set; }
        public string OldProfileName { get; set; }
        public string NewProfileName { get; set; }
        public ObjectId NewProfileId { get; set; }
    }
    
    public partial class ProfileBandChangeForm : Form
    {
        public ObjectId SelectedProfileViewId { get; private set; }
        public ProfileChangeSettings Profile1Settings { get; private set; }
        public ProfileChangeSettings Profile2Settings { get; private set; }
        
        private WinComboBox cmbProfileView;
        private WinCheckBox chkChangeProfile1;
        private WinLabel lblProfile1Current;
        private WinComboBox cmbProfile1New;
        private WinButton btnSelectProfile1;
        private WinCheckBox chkChangeProfile2;
        private WinLabel lblProfile2Current;
        private WinComboBox cmbProfile2New;
        private WinButton btnSelectProfile2;
        private WinButton btnOK;
        private WinButton btnCancel;
        private WinButton btnSelectProfileView;
        private WinLabel lblProfileView;
        private WinLabel lblProfile1New;
        private WinLabel lblProfile2New;
        
        private string currentProfile1Name = "";
        private string currentProfile2Name = "";
        
        public ProfileBandChangeForm()
        {
            Profile1Settings = new ProfileChangeSettings();
            Profile2Settings = new ProfileChangeSettings();
            InitializeComponent();
            LoadProfileViews();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Thay đổi Profile trong Band";
            this.Size = new System.Drawing.Size(550, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Label và ComboBox cho ProfileView
            lblProfileView = new WinLabel
            {
                Text = "Chọn ProfileView:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(120, 23)
            };
            
            cmbProfileView = new WinComboBox
            {
                Location = new System.Drawing.Point(150, 20),
                Size = new System.Drawing.Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbProfileView.SelectedIndexChanged += CmbProfileView_SelectedIndexChanged;
            
            // Nút chọn ProfileView từ model
            btnSelectProfileView = new WinButton
            {
                Text = "Chọn từ Model",
                Location = new System.Drawing.Point(410, 20),
                Size = new System.Drawing.Size(100, 23)
            };
            btnSelectProfileView.Click += BtnSelectProfileView_Click;
            
            // Profile 1 settings
            chkChangeProfile1 = new WinCheckBox
            {
                Text = "Thay đổi Profile 1:",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(150, 23),
                Checked = true
            };
            chkChangeProfile1.CheckedChanged += ChkChangeProfile1_CheckedChanged;
            
            lblProfile1Current = new WinLabel
            {
                Text = "Profile 1 hiện tại: (chưa chọn ProfileView)",
                Location = new System.Drawing.Point(40, 90),
                Size = new System.Drawing.Size(470, 23),
                ForeColor = System.Drawing.Color.Blue
            };
            
            lblProfile1New = new WinLabel
            {
                Text = "Profile 1 mới:",
                Location = new System.Drawing.Point(40, 120),
                Size = new System.Drawing.Size(80, 23)
            };
            
            cmbProfile1New = new WinComboBox
            {
                Location = new System.Drawing.Point(130, 120),
                Size = new System.Drawing.Size(270, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            btnSelectProfile1 = new WinButton
            {
                Text = "Chọn từ Model",
                Location = new System.Drawing.Point(410, 120),
                Size = new System.Drawing.Size(100, 23)
            };
            btnSelectProfile1.Click += BtnSelectProfile1_Click;
            
            // Profile 2 settings
            chkChangeProfile2 = new WinCheckBox
            {
                Text = "Thay đổi Profile 2:",
                Location = new System.Drawing.Point(20, 160),
                Size = new System.Drawing.Size(150, 23),
                Checked = false
            };
            chkChangeProfile2.CheckedChanged += ChkChangeProfile2_CheckedChanged;
            
            lblProfile2Current = new WinLabel
            {
                Text = "Profile 2 hiện tại: (chưa chọn ProfileView)",
                Location = new System.Drawing.Point(40, 190),
                Size = new System.Drawing.Size(470, 23),
                ForeColor = System.Drawing.Color.Blue,
                Enabled = false
            };
            
            lblProfile2New = new WinLabel
            {
                Text = "Profile 2 mới:",
                Location = new System.Drawing.Point(40, 220),
                Size = new System.Drawing.Size(80, 23),
                Enabled = false
            };
            
            cmbProfile2New = new WinComboBox
            {
                Location = new System.Drawing.Point(130, 220),
                Size = new System.Drawing.Size(270, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            
            btnSelectProfile2 = new WinButton
            {
                Text = "Chọn từ Model",
                Location = new System.Drawing.Point(410, 220),
                Size = new System.Drawing.Size(100, 23),
                Enabled = false
            };
            btnSelectProfile2.Click += BtnSelectProfile2_Click;
            
            // Buttons
            btnOK = new WinButton
            {
                Text = "OK",
                Location = new System.Drawing.Point(345, 270),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;
            
            btnCancel = new WinButton
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(435, 270),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.Cancel
            };
            
            // Add controls to form
            this.Controls.Add(lblProfileView);
            this.Controls.Add(cmbProfileView);
            this.Controls.Add(btnSelectProfileView);
            
            this.Controls.Add(chkChangeProfile1);
            this.Controls.Add(lblProfile1Current);
            this.Controls.Add(lblProfile1New);
            this.Controls.Add(cmbProfile1New);
            this.Controls.Add(btnSelectProfile1);
            
            this.Controls.Add(chkChangeProfile2);
            this.Controls.Add(lblProfile2Current);
            this.Controls.Add(lblProfile2New);
            this.Controls.Add(cmbProfile2New);
            this.Controls.Add(btnSelectProfile2);
            
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
        }
        
        private void ChkChangeProfile1_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkChangeProfile1.Checked;
            lblProfile1New.Enabled = enabled;
            cmbProfile1New.Enabled = enabled;
            btnSelectProfile1.Enabled = enabled;
        }
        
        private void ChkChangeProfile2_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkChangeProfile2.Checked;
            lblProfile2Current.Enabled = enabled;
            lblProfile2New.Enabled = enabled;
            cmbProfile2New.Enabled = enabled;
            btnSelectProfile2.Enabled = enabled;
        }
        
        private void BtnSelectProfileView_Click(object sender, EventArgs e)
        {
            try
            {
                // Đóng form tạm thời
                this.Hide();
                
                // Gọi trực tiếp hàm chọn ProfileView
                ObjectId selectedId = ObjectId.Null;
                
                // Thực hiện selection trong try-catch riêng
                try
                {
                    selectedId = UserInput.GProfileViewId("\nChọn ProfileView từ model:");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi chọn ProfileView: {ex.Message}");
                }
                
                // Hiển thị lại form
                this.Show();
                this.BringToFront();
                this.Activate();
                
                // Xử lý kết quả selection
                if (selectedId != ObjectId.Null)
                {
                    AddSelectedProfileViewToList(selectedId);
                }
            }
            catch (System.Exception ex)
            {
                // Đảm bảo form được hiển thị lại
                this.Show();
                this.BringToFront();
                MessageBox.Show($"Lỗi trong quá trình chọn: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnSelectProfile1_Click(object sender, EventArgs e)
        {
            try
            {
                this.Hide();
                ObjectId selectedId = ObjectId.Null;
                
                try
                {
                    selectedId = UserInput.GProfileId("\nChọn Profile mới cho Profile 1:");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi chọn Profile: {ex.Message}");
                }
                
                this.Show();
                this.BringToFront();
                this.Activate();
                
                if (selectedId != ObjectId.Null)
                {
                    AddSelectedProfileToList(selectedId, cmbProfile1New);
                }
            }
            catch (System.Exception ex)
            {
                this.Show();
                this.BringToFront();
                MessageBox.Show($"Lỗi trong quá trình chọn Profile 1: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnSelectProfile2_Click(object sender, EventArgs e)
        {
            try
            {
                this.Hide();
                ObjectId selectedId = ObjectId.Null;
                
                try
                {
                    selectedId = UserInput.GProfileId("\nChọn Profile mới cho Profile 2:");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi chọn Profile: {ex.Message}");
                }
                
                this.Show();
                this.BringToFront();
                this.Activate();
                
                if (selectedId != ObjectId.Null)
                {
                    AddSelectedProfileToList(selectedId, cmbProfile2New);
                }
            }
            catch (System.Exception ex)
            {
                this.Show();
                this.BringToFront();
                MessageBox.Show($"Lỗi trong quá trình chọn Profile 2: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void AddSelectedProfileToList(ObjectId profileId, WinComboBox targetComboBox)
        {
            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                    if (profile != null)
                    {
                        ProfileItem item = new ProfileItem
                        {
                            Name = profile.Name,
                            Id = profileId
                        };
                        
                        // Kiểm tra xem item đã tồn tại chưa
                        bool exists = false;
                        foreach (ProfileItem existingItem in targetComboBox.Items)
                        {
                            if (existingItem.Id == profileId)
                            {
                                exists = true;
                                targetComboBox.SelectedItem = existingItem;
                                break;
                            }
                        }
                        
                        if (!exists)
                        {
                            targetComboBox.Items.Add(item);
                            targetComboBox.SelectedItem = item;
                        }
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm Profile: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void LoadCurrentProfilesFromBand(ObjectId profileViewId)
        {
            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;
                    if (profileView != null)
                    {
                        // Reset thông tin hiện tại
                        currentProfile1Name = "";
                        currentProfile2Name = "";
                        
                        // Kiểm tra Bottom Bands trước
                        try
                        {
                            ProfileViewBandItemCollection bottomBands = profileView.Bands.GetBottomBandItems();
                            for (int i = 0; i < bottomBands.Count; i++)
                            {
                                var bandItem = bottomBands[i];
                                
                                // Lấy Profile1 nếu có
                                if (string.IsNullOrEmpty(currentProfile1Name) && bandItem.Profile1Id != ObjectId.Null)
                                {
                                    Profile? profile1 = tr.GetObject(bandItem.Profile1Id, OpenMode.ForRead) as Profile;
                                    if (profile1 != null)
                                    {
                                        currentProfile1Name = profile1.Name;
                                    }
                                }
                                
                                // Lấy Profile2 nếu có
                                if (string.IsNullOrEmpty(currentProfile2Name) && bandItem.Profile2Id != ObjectId.Null)
                                {
                                    Profile? profile2 = tr.GetObject(bandItem.Profile2Id, OpenMode.ForRead) as Profile;
                                    if (profile2 != null)
                                    {
                                        currentProfile2Name = profile2.Name;
                                    }
                                }
                                
                                // Nếu đã tìm thấy cả 2 thì dừng
                                if (!string.IsNullOrEmpty(currentProfile1Name) && !string.IsNullOrEmpty(currentProfile2Name))
                                    break;
                            }
                        }
                        catch { }
                        
                        // Nếu chưa tìm thấy, kiểm tra Top Bands
                        if (string.IsNullOrEmpty(currentProfile1Name) || string.IsNullOrEmpty(currentProfile2Name))
                        {
                            try
                            {
                                ProfileViewBandItemCollection topBands = profileView.Bands.GetTopBandItems();
                                for (int i = 0; i < topBands.Count; i++)
                                {
                                    var bandItem = topBands[i];
                                    
                                    // Lấy Profile1 nếu có và chưa tìm thấy
                                    if (string.IsNullOrEmpty(currentProfile1Name) && bandItem.Profile1Id != ObjectId.Null)
                                    {
                                        Profile? profile1 = tr.GetObject(bandItem.Profile1Id, OpenMode.ForRead) as Profile;
                                        if (profile1 != null)
                                        {
                                            currentProfile1Name = profile1.Name;
                                        }
                                    }
                                    
                                    // Lấy Profile2 nếu có và chưa tìm thấy
                                    if (string.IsNullOrEmpty(currentProfile2Name) && bandItem.Profile2Id != ObjectId.Null)
                                    {
                                        Profile? profile2 = tr.GetObject(bandItem.Profile2Id, OpenMode.ForRead) as Profile;
                                        if (profile2 != null)
                                        {
                                            currentProfile2Name = profile2.Name;
                                        }
                                    }
                                    
                                    // Nếu đã tìm thấy cả 2 thì dừng
                                    if (!string.IsNullOrEmpty(currentProfile1Name) && !string.IsNullOrEmpty(currentProfile2Name))
                                        break;
                                }
                            }
                            catch { }
                        }
                        
                        // Cập nhật hiển thị
                        UpdateCurrentProfileLabels();
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi đọc profile từ band: {ex.Message}");
            }
        }
        
        private void UpdateCurrentProfileLabels()
        {
            // Cập nhật label cho Profile 1
            if (!string.IsNullOrEmpty(currentProfile1Name))
            {
                lblProfile1Current.Text = $"Profile 1 hiện tại: {currentProfile1Name}";
                lblProfile1Current.ForeColor = System.Drawing.Color.DarkGreen;
            }
            else
            {
                lblProfile1Current.Text = "Profile 1 hiện tại: (không tìm thấy)";
                lblProfile1Current.ForeColor = System.Drawing.Color.Red;
            }
            
            // Cập nhật label cho Profile 2
            if (!string.IsNullOrEmpty(currentProfile2Name))
            {
                lblProfile2Current.Text = $"Profile 2 hiện tại: {currentProfile2Name}";
                lblProfile2Current.ForeColor = System.Drawing.Color.DarkGreen;
            }
            else
            {
                lblProfile2Current.Text = "Profile 2 hiện tại: (không tìm thấy)";
                lblProfile2Current.ForeColor = System.Drawing.Color.Gray;
            }
        }
        
        private void AddSelectedProfileViewToList(ObjectId profileViewId)
        {
            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;
                    if (profileView != null)
                    {
                        // Lấy alignment tương ứng
                        Alignment? alignment = tr.GetObject(profileView.AlignmentId, OpenMode.ForRead) as Alignment;
                        
                        ProfileViewItem item = new ProfileViewItem
                        {
                            Name = $"{profileView.Name} ({alignment?.Name ?? "Unknown"})",
                            Id = profileViewId,
                            AlignmentId = profileView.AlignmentId
                        };
                        
                        // Kiểm tra xem item đã tồn tại chưa
                        bool exists = false;
                        foreach (ProfileViewItem existingItem in cmbProfileView.Items)
                        {
                            if (existingItem.Id == profileViewId)
                            {
                                exists = true;
                                // Chọn item đã tồn tại
                                cmbProfileView.SelectedItem = existingItem;
                                break;
                            }
                        }
                        
                        if (!exists)
                        {
                            cmbProfileView.Items.Add(item);
                            // Chọn item mới
                            cmbProfileView.SelectedItem = item;
                        }
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm ProfileView: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void LoadProfileViews()
        {
            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    // Lấy tất cả ProfileView trong bản vẽ
                    ObjectIdCollection alignmentIds = A.Cdoc.GetAlignmentIds();
                    
                    foreach (ObjectId alignmentId in alignmentIds)
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment != null)
                        {
                            ObjectIdCollection profileViewIds = alignment.GetProfileViewIds();
                            foreach (ObjectId profileViewId in profileViewIds)
                            {
                                ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;
                                if (profileView != null)
                                {
                                    ProfileViewItem item = new ProfileViewItem
                                    {
                                        Name = $"{profileView.Name} ({alignment.Name})",
                                        Id = profileViewId,
                                        AlignmentId = alignmentId
                                    };
                                    cmbProfileView.Items.Add(item);
                                }
                            }
                        }
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải ProfileView: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void CmbProfileView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbProfileView.SelectedItem is ProfileViewItem selectedItem)
            {
                LoadProfilesForAlignment(selectedItem.AlignmentId);
                // Đọc thông tin profile hiện tại từ band
                LoadCurrentProfilesFromBand(selectedItem.Id);
            }
        }
        
        private void LoadProfilesForAlignment(ObjectId alignmentId)
        {
            cmbProfile1New.Items.Clear();
            cmbProfile2New.Items.Clear();
            
            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                    if (alignment != null)
                    {
                        ObjectIdCollection profileIds = alignment.GetProfileIds();
                        foreach (ObjectId profileId in profileIds)
                        {
                            Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                            if (profile != null)
                            {
                                ProfileItem item = new ProfileItem
                                {
                                    Name = profile.Name,
                                    Id = profileId
                                };
                                cmbProfile1New.Items.Add(item);
                                cmbProfile2New.Items.Add(item);
                            }
                        }
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải Profile: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (!(cmbProfileView.SelectedItem is ProfileViewItem profileViewItem))
            {
                MessageBox.Show("Vui lòng chọn ProfileView!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }
            
            // Kiểm tra Profile 1 settings
            if (chkChangeProfile1.Checked)
            {
                if (!(cmbProfile1New.SelectedItem is ProfileItem profile1NewItem))
                {
                    MessageBox.Show("Vui lòng chọn Profile 1 mới!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                
                Profile1Settings.ShouldChange = true;
                Profile1Settings.OldProfileName = currentProfile1Name; // Có thể rỗng nếu không tìm thấy
                Profile1Settings.NewProfileName = profile1NewItem.Name;
                Profile1Settings.NewProfileId = profile1NewItem.Id;
            }
            else
            {
                Profile1Settings.ShouldChange = false;
            }
            
            // Kiểm tra Profile 2 settings
            if (chkChangeProfile2.Checked)
            {
                if (!(cmbProfile2New.SelectedItem is ProfileItem profile2NewItem))
                {
                    MessageBox.Show("Vui lòng chọn Profile 2 mới!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                
                Profile2Settings.ShouldChange = true;
                Profile2Settings.OldProfileName = currentProfile2Name; // Có thể rỗng nếu không tìm thấy
                Profile2Settings.NewProfileName = profile2NewItem.Name;
                Profile2Settings.NewProfileId = profile2NewItem.Id;
            }
            else
            {
                Profile2Settings.ShouldChange = false;
            }
            
            // Kiểm tra ít nhất một profile được chọn để thay đổi
            if (!Profile1Settings.ShouldChange && !Profile2Settings.ShouldChange)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một profile để thay đổi!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }
            
            SelectedProfileViewId = profileViewItem.Id;
        }
    }
    
    public class ProfileViewItem
    {
        public string Name { get; set; }
        public ObjectId Id { get; set; }
        public ObjectId AlignmentId { get; set; }
        
        public override string ToString()
        {
            return Name;
        }
    }
    
    public class ProfileItem
    {
        public string Name { get; set; }
        public ObjectId Id { get; set; }
        
        public override string ToString()
        {
            return Name;
        }
    }
}

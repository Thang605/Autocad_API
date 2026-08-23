using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Form nhập thông số cho lệnh Gắn Nhãn Nút Giao Lên Trắc Dọc
    /// Hỗ trợ chọn PointGroup hoặc quét chọn trực tiếp CogoPoint trên bản vẽ
    /// </summary>
    public class GanNhanNutGiaoForm : Form
    {
        // Static variables to remember last input values
        private static string _lastPointGroupName = "";
        private static bool _lastIsCustomSelection = false;
        private static bool _lastLimitOffset = false; // Mặc định không giới hạn offset để gắn đủ tất cả điểm
        private static double _lastSaiSo = 10.0;     // Nới rộng sai số mặc định nếu có tích chọn
        private static bool _lastFilterStationRange = true;
        private static List<string> _lastSelectedProfileViews = new List<string>();

        // Properties to return data
        public ObjectId SelectedPointGroupId { get; private set; } = ObjectId.Null;
        public bool IsCustomSelection { get; private set; } = false;
        public List<ObjectId> CustomSelectedPointIds { get; private set; } = new List<ObjectId>();
        public bool LimitOffset { get; private set; } = false;
        public double SaiSo { get; private set; } = 10.0;
        public bool FilterByStationRange { get; private set; } = true;
        public List<ObjectId> SelectedProfileViewIds { get; private set; } = new List<ObjectId>();
        public bool FormAccepted { get; private set; } = false;

        // Info classes
        private class PointGroupInfo
        {
            public string Name { get; set; } = "";
            public ObjectId Id { get; set; }
            public override string ToString() => Name;
        }

        private class ProfileViewInfo
        {
            public string Name { get; set; } = "";
            public ObjectId Id { get; set; }
            public override string ToString() => Name;
        }

        // UI Controls
        private WinFormsLabel lblTitle = null!;
        private GroupBox grpPointSource = null!;
        private RadioButton radPointGroup = null!;
        private ComboBox cmbPointGroup = null!;
        private RadioButton radPickScreen = null!;
        private Button btnPickScreen = null!;
        private WinFormsLabel lblPickCount = null!;

        private GroupBox grpFilter = null!;
        private CheckBox chkLimitOffset = null!;
        private WinFormsLabel lblSaiSo = null!;
        private NumericUpDown numSaiSo = null!;
        private CheckBox chkFilterStationRange = null!;

        private GroupBox grpProfileViews = null!;
        private WinFormsLabel lblProfileViews = null!;
        private WinFormsLabel lblCount = null!;
        private ListBox lstProfileViews = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;

        private Button btnOK = null!;
        private Button btnCancel = null!;

        public GanNhanNutGiaoForm()
        {
            InitializeComponent();
            LoadPointGroups();
            LoadProfileViews();
            RestoreLastUsedValues();
            UpdateUIState();
            UpdateSelectedCount();
        }

        private void InitializeComponent()
        {
            this.lblTitle = new WinFormsLabel();

            // Point Source Group
            this.grpPointSource = new GroupBox();
            this.radPointGroup = new RadioButton();
            this.cmbPointGroup = new ComboBox();
            this.radPickScreen = new RadioButton();
            this.btnPickScreen = new Button();
            this.lblPickCount = new WinFormsLabel();

            // Filter Group
            this.grpFilter = new GroupBox();
            this.chkLimitOffset = new CheckBox();
            this.lblSaiSo = new WinFormsLabel();
            this.numSaiSo = new NumericUpDown();
            this.chkFilterStationRange = new CheckBox();

            // ProfileViews Group
            this.grpProfileViews = new GroupBox();
            this.lblProfileViews = new WinFormsLabel();
            this.lstProfileViews = new ListBox();
            this.lblCount = new WinFormsLabel();
            this.btnSelectAll = new Button();
            this.btnDeselectAll = new Button();

            // Action Buttons
            this.btnOK = new Button();
            this.btnCancel = new Button();

            this.SuspendLayout();

            // Form settings
            this.Text = "Gắn Nhãn Nút Giao / CogoPoint Lên Trắc Dọc";
            this.Size = new Size(540, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            // Title Label
            this.lblTitle.Text = "GẮN NHÃN NÚT GIAO LÊN TRẮC DỌC";
            this.lblTitle.Font = new WinFormsFont("Segoe UI", 11F, FontStyle.Bold);
            this.lblTitle.Location = new WinFormsPoint(15, 10);
            this.lblTitle.Size = new Size(495, 25);
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTitle.ForeColor = Color.DarkBlue;

            // 1. Group Point Source
            this.grpPointSource.Text = "1. Nguồn điểm CogoPoint";
            this.grpPointSource.Location = new WinFormsPoint(15, 40);
            this.grpPointSource.Size = new Size(495, 95);

            this.radPointGroup.Text = "Theo Point Group:";
            this.radPointGroup.Location = new WinFormsPoint(15, 24);
            this.radPointGroup.Size = new Size(130, 24);
            this.radPointGroup.Checked = true;
            this.radPointGroup.CheckedChanged += (s, e) => UpdateUIState();

            this.cmbPointGroup.Location = new WinFormsPoint(150, 24);
            this.cmbPointGroup.Size = new Size(325, 24);
            this.cmbPointGroup.DropDownStyle = ComboBoxStyle.DropDownList;

            this.radPickScreen.Text = "Quét chọn trên bản vẽ:";
            this.radPickScreen.Location = new WinFormsPoint(15, 58);
            this.radPickScreen.Size = new Size(160, 24);
            this.radPickScreen.CheckedChanged += (s, e) => UpdateUIState();

            this.btnPickScreen.Text = "Chọn điểm (Pick)...";
            this.btnPickScreen.Location = new WinFormsPoint(180, 56);
            this.btnPickScreen.Size = new Size(130, 26);
            this.btnPickScreen.Click += BtnPickScreen_Click;

            this.lblPickCount.Text = "Chưa chọn điểm nào";
            this.lblPickCount.Location = new WinFormsPoint(318, 60);
            this.lblPickCount.Size = new Size(160, 20);
            this.lblPickCount.ForeColor = Color.Gray;

            this.grpPointSource.Controls.AddRange(new Control[] {
                radPointGroup, cmbPointGroup,
                radPickScreen, btnPickScreen, lblPickCount
            });

            // 2. Group Filter
            this.grpFilter.Text = "2. Điều kiện lọc điểm";
            this.grpFilter.Location = new WinFormsPoint(15, 142);
            this.grpFilter.Size = new Size(495, 85);

            this.chkLimitOffset.Text = "Giới hạn khoảng cách lệch tim tối đa:";
            this.chkLimitOffset.Location = new WinFormsPoint(15, 23);
            this.chkLimitOffset.Size = new Size(235, 24);
            this.chkLimitOffset.Checked = false;
            this.chkLimitOffset.CheckedChanged += (s, e) => {
                numSaiSo.Enabled = chkLimitOffset.Checked;
                lblSaiSo.Enabled = chkLimitOffset.Checked;
            };

            this.numSaiSo.Location = new WinFormsPoint(255, 23);
            this.numSaiSo.Size = new Size(90, 24);
            this.numSaiSo.Minimum = 0.001M;
            this.numSaiSo.Maximum = 1000M;
            this.numSaiSo.Value = 10.0M;
            this.numSaiSo.DecimalPlaces = 3;
            this.numSaiSo.Increment = 1.0M;
            this.numSaiSo.Enabled = false;

            this.lblSaiSo.Text = "(m)";
            this.lblSaiSo.Location = new WinFormsPoint(350, 25);
            this.lblSaiSo.Size = new Size(30, 20);
            this.lblSaiSo.Enabled = false;

            this.chkFilterStationRange.Text = "Chỉ lấy điểm trong phạm vi lý trình của từng Trắc dọc (ProfileView)";
            this.chkFilterStationRange.Location = new WinFormsPoint(15, 52);
            this.chkFilterStationRange.Size = new Size(450, 24);
            this.chkFilterStationRange.Checked = true;

            this.grpFilter.Controls.AddRange(new Control[] {
                chkLimitOffset, numSaiSo, lblSaiSo,
                chkFilterStationRange
            });

            // 3. Group ProfileViews
            this.grpProfileViews.Text = "3. Chọn các ProfileView cần gắn nhãn";
            this.grpProfileViews.Location = new WinFormsPoint(15, 234);
            this.grpProfileViews.Size = new Size(495, 260);

            this.lblProfileViews.Text = "Danh sách ProfileView (giữ phím Ctrl hoặc Shift để chọn nhiều):";
            this.lblProfileViews.Location = new WinFormsPoint(15, 22);
            this.lblProfileViews.Size = new Size(450, 20);

            this.lstProfileViews.Location = new WinFormsPoint(15, 45);
            this.lstProfileViews.Size = new Size(465, 160);
            this.lstProfileViews.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lstProfileViews.SelectedIndexChanged += LstProfileViews_SelectedIndexChanged;

            this.lblCount.Text = "Đã chọn: 0 ProfileView";
            this.lblCount.Location = new WinFormsPoint(15, 218);
            this.lblCount.Size = new Size(200, 23);
            this.lblCount.ForeColor = Color.DarkGreen;
            this.lblCount.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold);

            this.btnSelectAll.Text = "Chọn tất cả";
            this.btnSelectAll.Location = new WinFormsPoint(270, 215);
            this.btnSelectAll.Size = new Size(100, 30);
            this.btnSelectAll.Click += BtnSelectAll_Click;

            this.btnDeselectAll.Text = "Bỏ chọn";
            this.btnDeselectAll.Location = new WinFormsPoint(380, 215);
            this.btnDeselectAll.Size = new Size(100, 30);
            this.btnDeselectAll.Click += BtnDeselectAll_Click;

            this.grpProfileViews.Controls.AddRange(new Control[] {
                lblProfileViews, lstProfileViews,
                lblCount, btnSelectAll, btnDeselectAll
            });

            // OK & Cancel Buttons
            this.btnOK.Text = "OK";
            this.btnOK.Location = new WinFormsPoint(300, 508);
            this.btnOK.Size = new Size(100, 35);
            this.btnOK.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold);
            this.btnOK.Click += BtnOK_Click;

            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new WinFormsPoint(410, 508);
            this.btnCancel.Size = new Size(100, 35);
            this.btnCancel.Click += BtnCancel_Click;

            // Add all to form
            this.Controls.AddRange(new Control[] {
                lblTitle,
                grpPointSource,
                grpFilter,
                grpProfileViews,
                btnOK,
                btnCancel
            });

            this.ResumeLayout(false);
        }

        private void UpdateUIState()
        {
            bool isGroup = radPointGroup.Checked;
            cmbPointGroup.Enabled = isGroup;
            btnPickScreen.Enabled = !isGroup;
            lblPickCount.Enabled = !isGroup;
        }

        private void BtnPickScreen_Click(object? sender, EventArgs e)
        {
            this.Hide();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    var ed = doc.Editor;
                    var filter = new SelectionFilter(new TypedValue[] {
                        new TypedValue((int)DxfCode.Start, "AECC_COGO_POINT")
                    });
                    PromptSelectionOptions pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n Chọn các điểm CogoPoint trên bản vẽ: "
                    };
                    var psr = ed.GetSelection(pso, filter);
                    if (psr.Status == PromptStatus.OK && psr.Value.Count > 0)
                    {
                        CustomSelectedPointIds = new List<ObjectId>(psr.Value.GetObjectIds());
                        lblPickCount.Text = $"Đã chọn: {CustomSelectedPointIds.Count} điểm";
                        lblPickCount.ForeColor = Color.DarkGreen;
                        radPickScreen.Checked = true;
                    }
                    else
                    {
                        if (CustomSelectedPointIds.Count == 0)
                        {
                            lblPickCount.Text = "Chưa chọn điểm nào";
                            lblPickCount.ForeColor = Color.Gray;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi chọn điểm trên màn hình: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }

        private void LoadPointGroups()
        {
            cmbPointGroup.Items.Clear();
            try
            {
                var civilDoc = CivilApplication.ActiveDocument;
                var pointGroupIds = civilDoc.PointGroups;

                // Add "[Tất cả điểm CogoPoint trong bản vẽ]" as the first item
                cmbPointGroup.Items.Add(new PointGroupInfo
                {
                    Name = "[Tất cả CogoPoint trong bản vẽ]",
                    Id = ObjectId.Null
                });

                using (var tr = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (ObjectId pointGroupId in pointGroupIds)
                    {
                        var pointGroup = tr.GetObject(pointGroupId, OpenMode.ForRead) as PointGroup;
                        if (pointGroup != null)
                        {
                            var info = new PointGroupInfo
                            {
                                Name = pointGroup.Name,
                                Id = pointGroup.Id
                            };
                            cmbPointGroup.Items.Add(info);
                        }
                    }
                    tr.Commit();
                }

                if (cmbPointGroup.Items.Count > 0)
                {
                    cmbPointGroup.SelectedIndex = 0;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách Point Group: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadProfileViews()
        {
            lstProfileViews.Items.Clear();
            try
            {
                var civilDoc = CivilApplication.ActiveDocument;

                using (var tr = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    var alignmentIds = civilDoc.GetAlignmentIds();
                    foreach (ObjectId alignmentId in alignmentIds)
                    {
                        var alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment != null)
                        {
                            var profileViewIds = alignment.GetProfileViewIds();
                            foreach (ObjectId profileViewId in profileViewIds)
                            {
                                var profileView = tr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;
                                if (profileView != null)
                                {
                                    var info = new ProfileViewInfo
                                    {
                                        Name = $"{profileView.Name} ({alignment.Name})",
                                        Id = profileView.Id
                                    };
                                    lstProfileViews.Items.Add(info);
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách ProfileView: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreLastUsedValues()
        {
            try
            {
                chkLimitOffset.Checked = _lastLimitOffset;
                numSaiSo.Enabled = _lastLimitOffset;
                lblSaiSo.Enabled = _lastLimitOffset;
                numSaiSo.Value = (decimal)_lastSaiSo;
                chkFilterStationRange.Checked = _lastFilterStationRange;
            }
            catch
            {
                chkLimitOffset.Checked = false;
                numSaiSo.Value = 10.0M;
                chkFilterStationRange.Checked = true;
            }

            if (_lastIsCustomSelection && CustomSelectedPointIds.Count > 0)
            {
                radPickScreen.Checked = true;
            }
            else
            {
                radPointGroup.Checked = true;
                if (!string.IsNullOrEmpty(_lastPointGroupName))
                {
                    for (int i = 0; i < cmbPointGroup.Items.Count; i++)
                    {
                        if (cmbPointGroup.Items[i].ToString() == _lastPointGroupName)
                        {
                            cmbPointGroup.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            if (_lastSelectedProfileViews.Count > 0)
            {
                for (int i = 0; i < lstProfileViews.Items.Count; i++)
                {
                    if (_lastSelectedProfileViews.Contains(lstProfileViews.Items[i].ToString() ?? ""))
                    {
                        lstProfileViews.SetSelected(i, true);
                    }
                }
            }
        }

        private void SaveLastUsedValues()
        {
            _lastLimitOffset = chkLimitOffset.Checked;
            _lastSaiSo = (double)numSaiSo.Value;
            _lastFilterStationRange = chkFilterStationRange.Checked;
            _lastIsCustomSelection = radPickScreen.Checked;

            if (cmbPointGroup.SelectedItem is PointGroupInfo selected)
            {
                _lastPointGroupName = selected.Name;
            }

            _lastSelectedProfileViews.Clear();
            foreach (var item in lstProfileViews.SelectedItems)
            {
                _lastSelectedProfileViews.Add(item.ToString() ?? "");
            }
        }

        private void UpdateSelectedCount()
        {
            lblCount.Text = $"Đã chọn: {lstProfileViews.SelectedItems.Count} ProfileView";
        }

        private void LstProfileViews_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateSelectedCount();
        }

        private void BtnSelectAll_Click(object? sender, EventArgs e)
        {
            for (int i = 0; i < lstProfileViews.Items.Count; i++)
            {
                lstProfileViews.SetSelected(i, true);
            }
            UpdateSelectedCount();
        }

        private void BtnDeselectAll_Click(object? sender, EventArgs e)
        {
            lstProfileViews.ClearSelected();
            UpdateSelectedCount();
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            IsCustomSelection = radPickScreen.Checked;

            if (IsCustomSelection)
            {
                if (CustomSelectedPointIds.Count == 0)
                {
                    MessageBox.Show("Vui lòng nhấn nút 'Chọn điểm (Pick)...' để quét chọn các điểm CogoPoint trên bản vẽ.",
                        "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnPickScreen.Focus();
                    return;
                }
            }
            else
            {
                if (cmbPointGroup.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn Point Group hoặc chọn Tất cả CogoPoint.",
                        "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbPointGroup.Focus();
                    return;
                }
                var selectedPointGroup = (PointGroupInfo)cmbPointGroup.SelectedItem;
                SelectedPointGroupId = selectedPointGroup.Id;
            }

            if (lstProfileViews.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một ProfileView cần gắn nhãn.",
                    "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                lstProfileViews.Focus();
                return;
            }

            LimitOffset = chkLimitOffset.Checked;
            SaiSo = (double)numSaiSo.Value;
            FilterByStationRange = chkFilterStationRange.Checked;

            SelectedProfileViewIds.Clear();
            foreach (var item in lstProfileViews.SelectedItems)
            {
                if (item is ProfileViewInfo pvInfo)
                {
                    SelectedProfileViewIds.Add(pvInfo.Id);
                }
            }

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
}

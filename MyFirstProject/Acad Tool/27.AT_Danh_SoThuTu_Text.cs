// (C) Copyright 2024 by T27
// Lệnh đánh số thứ tự cho TEXT / MTEXT
// Bổ sung Form với tính năng: Tiền tố, Số bắt đầu, Hậu tố

using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_DanhSoThuTu_Text_Class))]

namespace Civil3DCsharp
{
    public class DanhSoTextForm : Form
    {
        private Label lblStartNum, lblPrefix, lblSuffix;
        private NumericUpDown numStart;
        private TextBox txtPrefix, txtSuffix;
        private Button btnOK, btnCancel;

        public int StartNumber { get; set; } = 1;
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";

        // Static fields để lưu lại cấu hình cũ
        public static int LastStartNumber = 1;
        public static string LastPrefix = "";
        public static string LastSuffix = "";

        public DanhSoTextForm()
        {
            this.Text = "🔢 Đánh số thứ tự TEXT";
            this.Size = new Size(320, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 20;

            lblStartNum = new Label { Text = "Số bắt đầu:", Location = new Point(20, y + 3), Size = new Size(80, 20) };
            numStart = new NumericUpDown { Location = new Point(110, y), Size = new Size(160, 23), Minimum = -9999, Maximum = 99999, Value = LastStartNumber };
            this.Controls.Add(lblStartNum); this.Controls.Add(numStart);

            y += 40;
            lblPrefix = new Label { Text = "Tiền tố:", Location = new Point(20, y + 3), Size = new Size(80, 20) };
            txtPrefix = new TextBox { Location = new Point(110, y), Size = new Size(160, 23), Text = LastPrefix };
            this.Controls.Add(lblPrefix); this.Controls.Add(txtPrefix);

            y += 40;
            lblSuffix = new Label { Text = "Hậu tố:", Location = new Point(20, y + 3), Size = new Size(80, 20) };
            txtSuffix = new TextBox { Location = new Point(110, y), Size = new Size(160, 23), Text = LastSuffix };
            this.Controls.Add(lblSuffix); this.Controls.Add(txtSuffix);

            y += 50;
            btnOK = new Button { Text = "✅ Bắt đầu", Location = new Point(50, y), Size = new Size(90, 30), DialogResult = DialogResult.OK };
            btnOK.Click += (s, e) => {
                StartNumber = (int)numStart.Value;
                Prefix = txtPrefix.Text;
                Suffix = txtSuffix.Text;

                LastStartNumber = StartNumber;
                LastPrefix = Prefix;
                LastSuffix = Suffix;
                
                this.Close();
            };

            btnCancel = new Button { Text = "❌ Hủy", Location = new Point(160, y), Size = new Size(90, 30), DialogResult = DialogResult.Cancel };

            this.Controls.Add(btnOK); this.Controls.Add(btnCancel);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    public class AT_DanhSoThuTu_Text_Class
    {
        [CommandMethod("AT_DanhSoThuTu")]
        public static void ET_DanhSoThuTu()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            using (var form = new DanhSoTextForm())
            {
                var result = Application.ShowModalDialog(form);
                if (result != DialogResult.OK)
                {
                    ed.WriteMessage("\n❌ Đã hủy lệnh.");
                    return;
                }

                int soThuTu = form.StartNumber;
                string prefix = form.Prefix;
                string suffix = form.Suffix;

                ed.WriteMessage($"\n=== 🔢 BẮT ĐẦU ĐÁNH SỐ THỨ TỰ: Tiền tố='{prefix}', Bắt đầu='{soThuTu}', Hậu tố='{suffix}' ===");
                ed.WriteMessage("\n(Nhấn ESC để kết thúc lệnh)");

                PromptEntityOptions peo = new PromptEntityOptions("\n📍 Chọn đối tượng TEXT/MTEXT tiếp theo:");
                peo.SetRejectMessage("\n⚠️ Vui lòng chọn TEXT hoặc MTEXT!");
                peo.AddAllowedClass(typeof(DBText), true);
                peo.AddAllowedClass(typeof(MText), true);

                while (true)
                {
                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status == PromptStatus.Cancel || per.Status == PromptStatus.None)
                        break;
                    if (per.Status != PromptStatus.OK)
                        continue;

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            var ent = tr.GetObject(per.ObjectId, OpenMode.ForWrite);
                            string newValue = $"{prefix}{soThuTu}{suffix}";

                            if (ent is DBText dbText)
                                dbText.TextString = newValue;
                            else if (ent is MText mText)
                                mText.Contents = newValue;

                            ed.WriteMessage($"\n   ✅ Đã cập nhật thành: {newValue}");
                            soThuTu++;
                            
                            // Tự động lưu lại số bắt đầu cho lần click kế tiếp nếu tắt lệnh và gọi lại
                            DanhSoTextForm.LastStartNumber = soThuTu;

                            tr.Commit();
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                        }
                    }
                }
                
                ed.WriteMessage("\n🔄 Đã kết thúc lệnh đánh số.");
            }
        }
    }
}

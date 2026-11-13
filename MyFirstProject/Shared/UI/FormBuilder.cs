using System;
using System.Drawing;
using System.Windows.Forms;

using Font = System.Drawing.Font;
using FlowDirection = System.Windows.Forms.FlowDirection;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Shared.UI
{
    /// <summary>Kiểu nút bấm chuẩn của dự án.</summary>
    public enum ButtonKind
    {
        /// <summary>Nút thường (Hủy, Đóng, Chọn trên bản vẽ...).</summary>
        Normal,
        /// <summary>Nút thực thi chính (Thực hiện, OK) - nền xanh, chữ đậm.</summary>
        Primary,
        /// <summary>Nút thao tác nguy hiểm (Xóa...) - nền đỏ.</summary>
        Danger
    }

    /// <summary>
    /// Bộ font Segoe UI dùng chung toàn plugin. Font được tạo 1 lần và sống suốt phiên AutoCAD,
    /// nên KHÔNG cần (và không được) Dispose trong từng Form - tránh rò rỉ GDI khi mở/đóng Form nhiều lần.
    /// </summary>
    public static class StandardFonts
    {
        private static Font? _regular, _bold, _title, _small;

        public static Font Regular => _regular ??= new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Bold => _bold ??= new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Title => _title ??= new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Small => _small ??= new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
    }

    /// <summary>
    /// Bộ dựng giao diện WinForms chuẩn (quy tắc GEMINI.md mục 1): Segoe UI, nút cao >= 40px,
    /// UseMnemonic = false, hỗ trợ resize và High-DPI.
    /// <para>Thứ tự dùng trong <c>BuildLayout()</c> của Form:
    /// <c>ApplyStandardStyle</c> (đầu tiên) → tạo control / <see cref="FieldGroup"/> → <c>BuildBody</c> (cuối cùng).</para>
    /// </summary>
    public static class FormBuilder
    {
        public const int ButtonHeight = 40;

        private static readonly Color PrimaryBack = Color.FromArgb(0, 120, 215);
        private static readonly Color DangerBack = Color.FromArgb(196, 43, 28);

        /// <summary>
        /// Thiết lập chuẩn cho Form dựng bằng code. Gọi ĐẦU TIÊN trong BuildLayout (có SuspendLayout).
        /// </summary>
        public static void ApplyStandardStyle(Form form, string title, Size minimumSize)
        {
            form.SuspendLayout();
            form.Text = title;
            form.Font = StandardFonts.Regular;
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.Sizable;
            form.MinimizeBox = false;
            form.MaximizeBox = true;
            form.ShowInTaskbar = false;
            form.ShowIcon = false;
            form.MinimumSize = minimumSize;
            form.Size = minimumSize;
            form.Padding = new Padding(8);
        }

        /// <summary>
        /// Lắp ráp thân Form: các nhóm (<paramref name="sections"/>) xếp từ trên xuống trong vùng cuộn,
        /// thanh nút (<paramref name="buttonBar"/>) cố định ở đáy. Gọi CUỐI CÙNG trong BuildLayout (có ResumeLayout).
        /// </summary>
        public static void BuildBody(Form form, Control buttonBar, params Control[] sections)
        {
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0, 0, 4, 0)
            };

            // Dock = Top xử lý theo thứ tự ngược: control thêm sau nằm trên → thêm ngược để sections[0] ở trên cùng.
            for (int i = sections.Length - 1; i >= 0; i--)
            {
                sections[i].Dock = DockStyle.Top;
                body.Controls.Add(sections[i]);
            }

            // Fill phải thêm trước Bottom để chiếm phần còn lại sau khi thanh nút đã neo đáy.
            form.Controls.Add(body);
            form.Controls.Add(buttonBar);

            form.AutoScaleDimensions = new SizeF(96F, 96F);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.ResumeLayout(false);
            form.PerformLayout();
        }

        /// <summary>Nút bấm chuẩn: cao 40px, tự giãn theo chữ (không bị cắt chữ), UseMnemonic = false.</summary>
        public static WinFormsButton ActionButton(string text, ButtonKind kind = ButtonKind.Normal, int minWidth = 120)
        {
            var btn = new WinFormsButton
            {
                Text = text,
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(minWidth, ButtonHeight),
                Size = new Size(minWidth, ButtonHeight),
                Margin = new Padding(6, 4, 0, 4),
                Padding = new Padding(10, 0, 10, 0)
            };

            if (kind == ButtonKind.Normal)
            {
                btn.UseVisualStyleBackColor = true;
                return btn;
            }

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.ForeColor = Color.White;
            btn.BackColor = kind == ButtonKind.Primary ? PrimaryBack : DangerBack;
            btn.Font = StandardFonts.Bold;
            return btn;
        }

        /// <summary>Thanh nút neo đáy Form, căn phải. Nút truyền vào hiển thị theo đúng thứ tự trái → phải.</summary>
        public static FlowLayoutPanel ButtonBar(params WinFormsButton[] buttons)
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 8, 0, 0)
            };

            // RightToLeft đặt control đầu tiên ở mép phải → thêm ngược để giữ thứ tự trái → phải.
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                bar.Controls.Add(buttons[i]);
            }
            return bar;
        }

        /// <summary>Nhãn chuẩn: AutoSize, UseMnemonic = false (không nuốt ký tự &amp;).</summary>
        public static WinFormsLabel Label(string text, bool bold = false)
        {
            return new WinFormsLabel
            {
                Text = text,
                AutoSize = true,
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 8, 6),
                Font = bold ? StandardFonts.Bold : null
            };
        }

        /// <summary>CheckBox chuẩn (AutoSize, UseMnemonic = false).</summary>
        public static CheckBox CheckBox(string text, bool isChecked = false)
        {
            return new CheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 3, 6)
            };
        }

        /// <summary>RadioButton chuẩn (AutoSize, UseMnemonic = false).</summary>
        public static RadioButton Radio(string text, bool isChecked = false)
        {
            return new RadioButton
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 12, 6)
            };
        }

        /// <summary>Ô nhập số chuẩn (căn phải, có giới hạn min/max).</summary>
        public static NumericUpDown Number(decimal min, decimal max, decimal value, int decimalPlaces = 2, decimal increment = 1m)
        {
            var nud = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimalPlaces,
                Increment = increment,
                TextAlign = HorizontalAlignment.Right,
                ThousandsSeparator = true,
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            nud.Value = Math.Min(max, Math.Max(min, value));
            return nud;
        }

        /// <summary>
        /// Tạo ToolTip gắn vào Form và tự Dispose khi Form bị Dispose.
        /// </summary>
        public static ToolTip CreateToolTip(Form form)
        {
            var toolTip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };
            form.Disposed += (s, e) => toolTip.Dispose();
            return toolTip;
        }
    }
}

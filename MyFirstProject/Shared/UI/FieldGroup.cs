using System.Windows.Forms;

using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Shared.UI
{
    /// <summary>
    /// Một nhóm thông số (GroupBox) dạng lưới 3 cột: [Nhãn] [Control giãn ngang] [Nút phụ, ví dụ Pick].
    /// Tự co giãn chiều cao theo nội dung, chiều ngang theo Form.
    /// <code>
    /// var grp = new FieldGroup("2. Tùy chọn", _toolTip)
    ///     .AddRow("Layer đích:", cboLayer, btnPickLayer, "Layer gán cho đối tượng kết quả")
    ///     .AddFullRow(chkZoom);
    /// </code>
    /// </summary>
    public sealed class FieldGroup
    {
        private readonly TableLayoutPanel _table;
        private readonly ToolTip? _toolTip;

        /// <summary>GroupBox để truyền vào <see cref="FormBuilder.BuildBody"/>.</summary>
        public GroupBox Box { get; }

        public FieldGroup(string title, ToolTip? toolTip = null)
        {
            _toolTip = toolTip;

            Box = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 0, 0, 8),
                Font = StandardFonts.Bold
            };

            _table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 0,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                Font = StandardFonts.Regular
            };
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Box.Controls.Add(_table);
        }

        /// <summary>Thêm 1 dòng: nhãn + control chính (giãn ngang) + control phụ tùy chọn (nút Pick...).</summary>
        public FieldGroup AddRow(string label, Control field, Control? trailing = null, string? tooltip = null)
        {
            int row = NewRow();

            WinFormsLabel lbl = FormBuilder.Label(label);
            _table.Controls.Add(lbl, 0, row);

            if (field is ComboBox || field is TextBox)
            {
                field.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }
            _table.Controls.Add(field, 1, row);

            if (trailing != null)
            {
                trailing.Anchor = AnchorStyles.Right;
                _table.Controls.Add(trailing, 2, row);
            }

            SetToolTip(tooltip, lbl, field);
            return this;
        }

        /// <summary>Thêm 1 control chiếm trọn chiều ngang (CheckBox, ListView, ghi chú...).</summary>
        public FieldGroup AddFullRow(Control control, string? tooltip = null)
        {
            int row = NewRow();
            _table.Controls.Add(control, 0, row);
            _table.SetColumnSpan(control, 3);
            SetToolTip(tooltip, control);
            return this;
        }

        /// <summary>
        /// Thêm 1 control chiếm trọn chiều ngang và có chiều cao cố định (ListView, DataGridView, TextBox nhiều dòng).
        /// </summary>
        public FieldGroup AddFullRow(Control control, int height, string? tooltip = null)
        {
            control.Height = height;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            return AddFullRow(control, tooltip);
        }

        private int NewRow()
        {
            int row = _table.RowCount;
            _table.RowCount = row + 1;
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return row;
        }

        private void SetToolTip(string? tooltip, params Control[] controls)
        {
            if (_toolTip == null || string.IsNullOrWhiteSpace(tooltip)) return;
            foreach (Control c in controls)
            {
                _toolTip.SetToolTip(c, tooltip);
            }
        }
    }
}

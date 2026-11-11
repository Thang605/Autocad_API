using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

// Aliases để tránh xung đột WPF vs WinForms
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfGrid = System.Windows.Controls.Grid;
using WpfBorder = System.Windows.Controls.Border;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using WpfColumnDefinition = System.Windows.Controls.ColumnDefinition;
using WpfRowDefinition = System.Windows.Controls.RowDefinition;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfCursors = System.Windows.Input.Cursors;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace MyFirstProject
{
    /// <summary>
    /// WPF UserControl — giao diện Tool Palette Dark Theme cho AutoCAD / Civil 3D.
    /// Hiển thị danh sách lệnh phân nhóm theo Category, hỗ trợ lọc nhanh (Tất cả / Civil 3D / AutoCAD),
    /// tìm kiếm tiếng Việt có dấu & không dấu, xem video hướng dẫn và chạy lệnh trực tiếp.
    /// </summary>
    public class ToolPaletteControl : WpfUserControl
    {
        // ═══════════════════════════════════════════════════════════════
        //  THEME COLORS (AutoCAD Dark)
        // ═══════════════════════════════════════════════════════════════

        static readonly SolidColorBrush BgMain = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E)));
        static readonly SolidColorBrush BgSearch = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x30, 0x30, 0x34)));
        static readonly SolidColorBrush BgInput = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x3C, 0x3C, 0x3C)));
        static readonly SolidColorBrush BgHeader = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x28, 0x28, 0x2C)));
        static readonly SolidColorBrush BgHeaderHover = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x33, 0x33, 0x38)));
        static readonly SolidColorBrush BgItem = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E)));
        static readonly SolidColorBrush BgItemHover = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x2D, 0x42, 0x5A)));
        static readonly SolidColorBrush BgItemClick = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x00, 0x6A, 0xB5)));
        static readonly SolidColorBrush BgTabActive = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x00, 0x7A, 0xCC)));
        static readonly SolidColorBrush BgTabInactive = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x2A, 0x2A, 0x2E)));
        static readonly SolidColorBrush FgText = Freeze(new SolidColorBrush(WpfColor.FromRgb(0xDC, 0xDC, 0xDC)));
        static readonly SolidColorBrush FgDim = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)));
        static readonly SolidColorBrush FgCmd = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x6A, 0x99, 0xBB)));
        static readonly SolidColorBrush FgCount = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x56, 0x9C, 0xD6)));
        static readonly SolidColorBrush AccentCivil = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x00, 0x7A, 0xCC)));
        static readonly SolidColorBrush AccentAcad = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x4E, 0xC9, 0xB0)));
        static readonly SolidColorBrush BorderLine = Freeze(new SolidColorBrush(WpfColor.FromRgb(0x3F, 0x3F, 0x46)));

        static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

        // ═══════════════════════════════════════════════════════════════
        //  STATE
        // ═══════════════════════════════════════════════════════════════

        private WpfTextBox _searchBox = null!;
        private WpfStackPanel _categoriesPanel = null!;
        private WpfTextBlock _statusText = null!;
        private WpfBorder _tabAll = null!;
        private WpfBorder _tabCivil = null!;
        private WpfBorder _tabAcad = null!;

        private List<PaletteCommandInfo> _allCommands = new();
        private readonly Action<string> _executeCommand;
        private readonly Action? _reloadCallback;
        private readonly Dictionary<string, bool> _expandStates = new();
        private string _activeScope = "ALL"; // "ALL", "CIVIL", "ACAD"

        // ═══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════

        public ToolPaletteControl(Action<string> executeCommand, Action? reloadCallback = null)
        {
            _executeCommand = executeCommand;
            _reloadCallback = reloadCallback;
            BuildUI();
        }

        // ═══════════════════════════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Background = BgMain;
            FontFamily = new WpfFontFamily("Segoe UI");

            var mainGrid = new WpfGrid();
            mainGrid.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });                      // Row 0: Search Bar
            mainGrid.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });                      // Row 1: Filter Tabs
            mainGrid.RowDefinitions.Add(new WpfRowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 2: Command List
            mainGrid.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });                      // Row 3: Status Bar

            // ─────────── ROW 0: SEARCH BAR ───────────
            var searchBorder = new WpfBorder
            {
                Background = BgSearch,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BorderLine,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var searchGrid = new WpfGrid();
            searchGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });

            var searchIcon = new WpfTextBlock
            {
                Text = "🔍",
                FontSize = 12,
                Foreground = FgDim,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            WpfGrid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            _searchBox = new WpfTextBox
            {
                Background = BgInput,
                Foreground = FgDim,
                CaretBrush = FgText,
                BorderBrush = BorderLine,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "placeholder",
                Text = "Tìm kiếm lệnh (tên, mã lệnh, mô tả)..."
            };

            _searchBox.GotFocus += (s, e) =>
            {
                if (_searchBox.Tag as string == "placeholder")
                {
                    _searchBox.Text = "";
                    _searchBox.Foreground = FgText;
                    _searchBox.Tag = null;
                }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(_searchBox.Text))
                {
                    _searchBox.Tag = "placeholder";
                    _searchBox.Text = "Tìm kiếm lệnh (tên, mã lệnh, mô tả)...";
                    _searchBox.Foreground = FgDim;
                }
            };
            _searchBox.TextChanged += (s, e) =>
            {
                if (_searchBox.Tag as string != "placeholder")
                    FilterCommands(_searchBox.Text);
            };
            WpfGrid.SetColumn(_searchBox, 1);
            searchGrid.Children.Add(_searchBox);

            var clearBtn = CreateIconButton("✕", 22, () =>
            {
                _searchBox.Tag = null;
                _searchBox.Text = "";
                _searchBox.Foreground = FgText;
                _searchBox.Focus();
                FilterCommands("");
            });
            clearBtn.ToolTip = "Xóa từ khóa tìm kiếm";
            clearBtn.Margin = new Thickness(4, 0, 0, 0);
            WpfGrid.SetColumn(clearBtn, 2);
            searchGrid.Children.Add(clearBtn);

            searchBorder.Child = searchGrid;
            WpfGrid.SetRow(searchBorder, 0);
            mainGrid.Children.Add(searchBorder);

            // ─────────── ROW 1: FILTER TABS (Tất cả / Civil 3D / AutoCAD) ───────────
            var tabBarBorder = new WpfBorder
            {
                Background = BgHeader,
                Padding = new Thickness(6, 4, 6, 4),
                BorderBrush = BorderLine,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var tabGrid = new WpfGrid();
            tabGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _tabAll = CreateScopeTab("Tất cả", "ALL");
            _tabCivil = CreateScopeTab("Civil 3D", "CIVIL");
            _tabAcad = CreateScopeTab("AutoCAD", "ACAD");

            WpfGrid.SetColumn(_tabAll, 0);
            WpfGrid.SetColumn(_tabCivil, 1);
            WpfGrid.SetColumn(_tabAcad, 2);

            tabGrid.Children.Add(_tabAll);
            tabGrid.Children.Add(_tabCivil);
            tabGrid.Children.Add(_tabAcad);

            tabBarBorder.Child = tabGrid;
            WpfGrid.SetRow(tabBarBorder, 1);
            mainGrid.Children.Add(tabBarBorder);

            UpdateTabVisuals();

            // ─────────── ROW 2: COMMANDS LIST ───────────
            var scrollViewer = new WpfScrollViewer
            {
                VerticalScrollBarVisibility = WpfScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = WpfScrollBarVisibility.Disabled,
                Background = BgMain,
                Focusable = false
            };

            _categoriesPanel = new WpfStackPanel();
            scrollViewer.Content = _categoriesPanel;

            WpfGrid.SetRow(scrollViewer, 2);
            mainGrid.Children.Add(scrollViewer);

            // ─────────── ROW 3: STATUS BAR ───────────
            var statusGrid = new WpfGrid();
            statusGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });
            statusGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });
            statusGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });

            _statusText = new WpfTextBlock
            {
                Foreground = FgDim,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };
            WpfGrid.SetColumn(_statusText, 0);
            statusGrid.Children.Add(_statusText);

            var reloadBtn = CreateIconButton("🔄", 22, () =>
            {
                _reloadCallback?.Invoke();
            });
            reloadBtn.ToolTip = "Nạp lại danh sách lệnh (RELOAD_PALETTE)";
            WpfGrid.SetColumn(reloadBtn, 1);
            statusGrid.Children.Add(reloadBtn);

            var expandAllBtn = CreateIconButton("▼", 22, () =>
            {
                foreach (var k in _expandStates.Keys.ToList()) _expandStates[k] = true;
                FilterCommands(GetSearchText());
            });
            expandAllBtn.ToolTip = "Mở rộng tất cả nhóm";
            expandAllBtn.Margin = new Thickness(2, 0, 0, 0);
            WpfGrid.SetColumn(expandAllBtn, 2);
            statusGrid.Children.Add(expandAllBtn);

            var collapseAllBtn = CreateIconButton("▶", 22, () =>
            {
                foreach (var k in _expandStates.Keys.ToList()) _expandStates[k] = false;
                FilterCommands(GetSearchText());
            });
            collapseAllBtn.ToolTip = "Thu gọn tất cả nhóm";
            collapseAllBtn.Margin = new Thickness(2, 0, 0, 0);
            WpfGrid.SetColumn(collapseAllBtn, 3);
            statusGrid.Children.Add(collapseAllBtn);

            var statusBorder = new WpfBorder
            {
                Background = BgSearch,
                BorderBrush = BorderLine,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 4, 6, 4),
                Child = statusGrid
            };
            WpfGrid.SetRow(statusBorder, 3);
            mainGrid.Children.Add(statusBorder);

            Content = mainGrid;
        }

        private WpfBorder CreateScopeTab(string text, string scope)
        {
            var tb = new WpfTextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgText,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new WpfBorder
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = WpfCursors.Hand,
                Child = tb
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                _activeScope = scope;
                UpdateTabVisuals();
                FilterCommands(GetSearchText());
                e.Handled = true;
            };

            return border;
        }

        private void UpdateTabVisuals()
        {
            if (_tabAll == null) return;
            _tabAll.Background = _activeScope == "ALL" ? BgTabActive : BgTabInactive;
            _tabCivil.Background = _activeScope == "CIVIL" ? BgTabActive : BgTabInactive;
            _tabAcad.Background = _activeScope == "ACAD" ? BgTabActive : BgTabInactive;
        }

        // ═══════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Nạp danh sách lệnh và render UI
        /// </summary>
        public void LoadCommands(List<PaletteCommandInfo> commands)
        {
            _allCommands = commands ?? new();

            foreach (var cat in _allCommands.Select(c => c.Category).Distinct())
            {
                if (!_expandStates.ContainsKey(cat))
                    _expandStates[cat] = true;
            }

            FilterCommands(GetSearchText());
        }

        // ═══════════════════════════════════════════════════════════════
        //  FILTER & RENDER
        // ═══════════════════════════════════════════════════════════════

        private static bool IsCivilCommand(PaletteCommandInfo c)
        {
            if (c.Category.StartsWith("Civil", StringComparison.OrdinalIgnoreCase))
                return true;
            if (c.Category.StartsWith("AutoCAD", StringComparison.OrdinalIgnoreCase) ||
                c.Category.StartsWith("Autocad", StringComparison.OrdinalIgnoreCase))
                return false;
            return c.Command.StartsWith("CT", StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (char ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch switch
                    {
                        'đ' => 'd',
                        'Đ' => 'D',
                        _ => ch
                    });
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private void FilterCommands(string searchText)
        {
            _categoriesPanel.Children.Clear();

            IEnumerable<PaletteCommandInfo> source = _allCommands;
            if (_activeScope == "CIVIL")
                source = source.Where(IsCivilCommand);
            else if (_activeScope == "ACAD")
                source = source.Where(c => !IsCivilCommand(c));

            List<PaletteCommandInfo> filtered;
            bool isSearching = !string.IsNullOrWhiteSpace(searchText);

            if (!isSearching)
            {
                filtered = source.ToList();
            }
            else
            {
                string query = searchText.Trim();
                string queryNoSign = RemoveDiacritics(query);

                filtered = source.Where(c =>
                    c.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Command.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    RemoveDiacritics(c.Label).Contains(queryNoSign, StringComparison.OrdinalIgnoreCase) ||
                    RemoveDiacritics(c.Description).Contains(queryNoSign, StringComparison.OrdinalIgnoreCase) ||
                    RemoveDiacritics(c.Category).Contains(queryNoSign, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Giữ nguyên thứ tự Category được định nghĩa trong file JSON / mặc định
            var grouped = filtered.GroupBy(c => c.Category);

            foreach (var group in grouped)
            {
                var section = CreateCategorySection(group.Key, group.ToList(), isSearching);
                _categoriesPanel.Children.Add(section);
            }

            UpdateStatus(filtered.Count);
        }

        // ═══════════════════════════════════════════════════════════════
        //  CATEGORY SECTION (custom collapsible)
        // ═══════════════════════════════════════════════════════════════

        private FrameworkElement CreateCategorySection(string category, List<PaletteCommandInfo> commands, bool forceExpand)
        {
            var container = new WpfStackPanel();

            bool isExpanded = forceExpand || (_expandStates.ContainsKey(category) && _expandStates[category]);
            bool isCivil = commands.Count > 0 && IsCivilCommand(commands[0]);

            var headerGrid = new WpfGrid();
            headerGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });

            // Left color indicator bar
            var accentRect = new WpfBorder
            {
                Width = 3,
                Background = isCivil ? AccentCivil : AccentAcad,
                Margin = new Thickness(0, 0, 6, 0)
            };
            WpfGrid.SetColumn(accentRect, 0);
            headerGrid.Children.Add(accentRect);

            // Arrow
            var arrow = new WpfTextBlock
            {
                Text = isExpanded ? "▼" : "▶",
                FontSize = 9,
                Foreground = FgDim,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 6, 0),
                Width = 12
            };
            WpfGrid.SetColumn(arrow, 1);
            headerGrid.Children.Add(arrow);

            // Category name
            var catName = new WpfTextBlock
            {
                Text = category,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            WpfGrid.SetColumn(catName, 2);
            headerGrid.Children.Add(catName);

            // Count badge
            var countText = new WpfTextBlock
            {
                Text = commands.Count.ToString(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgCount,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };
            WpfGrid.SetColumn(countText, 3);
            headerGrid.Children.Add(countText);

            var headerBorder = new WpfBorder
            {
                Background = BgHeader,
                Padding = new Thickness(0, 6, 0, 6),
                BorderBrush = BorderLine,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = WpfCursors.Hand,
                Child = headerGrid
            };

            var itemsPanel = new WpfStackPanel
            {
                Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed
            };

            foreach (var cmd in commands)
            {
                itemsPanel.Children.Add(CreateCommandItem(cmd));
            }

            headerBorder.MouseEnter += (s, e) => headerBorder.Background = BgHeaderHover;
            headerBorder.MouseLeave += (s, e) => headerBorder.Background = BgHeader;

            headerBorder.MouseLeftButtonDown += (s, e) =>
            {
                bool newState = itemsPanel.Visibility != Visibility.Visible;
                itemsPanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                arrow.Text = newState ? "▼" : "▶";
                _expandStates[category] = newState;
                e.Handled = true;
            };

            container.Children.Add(headerBorder);
            container.Children.Add(itemsPanel);
            return container;
        }

        // ═══════════════════════════════════════════════════════════════
        //  COMMAND ITEM
        // ═══════════════════════════════════════════════════════════════

        private WpfBorder CreateCommandItem(PaletteCommandInfo cmd)
        {
            var grid = new WpfGrid();
            grid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = GridLength.Auto });

            // Label
            var label = new WpfTextBlock
            {
                Text = cmd.Label,
                FontSize = 12,
                Foreground = FgText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            WpfGrid.SetColumn(label, 0);
            grid.Children.Add(label);

            // Optional Video button
            if (!string.IsNullOrWhiteSpace(cmd.VideoUrl))
            {
                string videoUrl = cmd.VideoUrl;
                var videoBtn = CreateIconButton("🎬", 20, () =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(videoUrl) { UseShellExecute = true });
                    }
                    catch { }
                });
                videoBtn.ToolTip = "Xem Video Hướng Dẫn";
                videoBtn.Margin = new Thickness(4, 0, 2, 0);
                WpfGrid.SetColumn(videoBtn, 1);
                grid.Children.Add(videoBtn);
            }

            // Command name (monospace, dim)
            var cmdText = new WpfTextBlock
            {
                Text = cmd.Command,
                FontSize = 10,
                FontFamily = new WpfFontFamily("Consolas"),
                Foreground = FgCmd,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            WpfGrid.SetColumn(cmdText, 2);
            grid.Children.Add(cmdText);

            var border = new WpfBorder
            {
                Background = BgItem,
                Padding = new Thickness(16, 6, 8, 6),
                BorderBrush = new SolidColorBrush(WpfColor.FromArgb(20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = WpfCursors.Hand,
                Child = grid
            };

            // Tooltip
            var tt = new WpfTextBlock
            {
                MaxWidth = 300,
                TextWrapping = TextWrapping.Wrap
            };
            if (!string.IsNullOrEmpty(cmd.Description))
            {
                tt.Inlines.Add(new System.Windows.Documents.Run(cmd.Description + "\n") { Foreground = FgText });
            }
            tt.Inlines.Add(new System.Windows.Documents.Run("Lệnh: " + cmd.Command + "\n")
            {
                Foreground = FgCmd,
                FontFamily = new WpfFontFamily("Consolas"),
                FontSize = 11
            });
            tt.Inlines.Add(new System.Windows.Documents.Run("(Chuột trái: Chạy lệnh | Chuột phải: Copy tên lệnh)")
            {
                Foreground = FgDim,
                FontSize = 10
            });
            border.ToolTip = tt;

            // Hover effect
            border.MouseEnter += (s, e) => border.Background = BgItemHover;
            border.MouseLeave += (s, e) => border.Background = BgItem;

            // Left Click → execute command
            border.MouseLeftButtonDown += (s, e) =>
            {
                border.Background = BgItemClick;
                e.Handled = true;
            };
            border.MouseLeftButtonUp += (s, e) =>
            {
                border.Background = BgItemHover;
                _executeCommand?.Invoke(cmd.Command);
                e.Handled = true;
            };

            // Right Click → copy command name to clipboard
            border.MouseRightButtonUp += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(cmd.Command);
                    _statusText.Text = $"📋 Đã copy: {cmd.Command}";
                }
                catch { }
                e.Handled = true;
            };

            return border;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private string GetSearchText()
        {
            if (_searchBox == null) return "";
            if (_searchBox.Tag as string == "placeholder") return "";
            return _searchBox.Text ?? "";
        }

        private void UpdateStatus(int visibleCount)
        {
            int total = _allCommands.Count;
            if (visibleCount != total)
                _statusText.Text = $"🔍 {visibleCount}/{total} lệnh";
            else
                _statusText.Text = $"📋 {total} lệnh";
        }

        private WpfButton CreateIconButton(string icon, double size, Action onClick)
        {
            var btn = new WpfButton
            {
                Content = icon,
                Width = size,
                Height = size,
                FontSize = 10,
                Padding = new Thickness(0),
                Background = WpfBrushes.Transparent,
                Foreground = FgDim,
                BorderThickness = new Thickness(0),
                Cursor = WpfCursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            btn.Click += (s, e) => onClick();
            btn.MouseEnter += (s, e) => btn.Foreground = FgText;
            btn.MouseLeave += (s, e) => btn.Foreground = FgDim;

            return btn;
        }
    }
}

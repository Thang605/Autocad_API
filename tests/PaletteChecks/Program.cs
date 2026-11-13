using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyFirstProject;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--snapshot")
                Snapshot(args[1]);
            else if (args.Contains("--benchmark"))
                Benchmark();
            else
                VerifyPalette();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyPalette()
    {
        var executed = new List<string>();
        var control = new ToolPaletteControl(executed.Add);
        var commands = new List<PaletteCommandInfo>
        {
            new() { Category = "Civil 3D - Trắc dọc", Label = "Điều chỉnh đường đỏ", Command = "CTP_Duong_Do", Description = "Độ dốc thiết kế" },
            new() { Category = "AutoCAD - Bản vẽ", Label = "Căn lề Text", Command = "AT_Can_Le", Description = "Sắp xếp ghi chú" },
            new() { Category = "Khác", Label = "Cọc", Command = "CTS_Coc", Description = "Lý trình" }
        };
        control.LoadCommands(commands);
        Check(VisibleCommands(control).SequenceEqual(commands.Select(c => c.Command)), "initial category/command order");
        AssertSearch(control, "  DUONG DO  ", "CTP_Duong_Do");
        AssertSearch(control, "ĐƯỜNG ĐỎ", "CTP_Duong_Do");
        AssertSearch(control, "đường đỏ".Normalize(System.Text.NormalizationForm.FormD), "CTP_Duong_Do");
        AssertSearch(control, "do doc", "CTP_Duong_Do");
        AssertSearch(control, "trac doc", "CTP_Duong_Do");
        AssertSearch(control, "at_can_le", "AT_Can_Le");
        AssertSearch(control, "khong-ton-tai");
        AssertSearch(control, "  ", "CTP_Duong_Do", "AT_Can_Le", "CTS_Coc");

        Click(Field<Border>(control, "_tabCivil"));
        Check(VisibleCommands(control).SequenceEqual(new[] { "CTP_Duong_Do", "CTS_Coc" }), "Civil scope and command-prefix fallback");
        Click(Field<Border>(control, "_tabAcad"));
        Check(VisibleCommands(control).SequenceEqual(new[] { "AT_Can_Le" }), "AutoCAD scope");
        Click(Field<Border>(control, "_tabAll"));

        var firstItem = Items(control).First();
        Search(control, "duong do");
        Search(control, "");
        Check(ReferenceEquals(firstItem, Items(control).First()), "rows reused between filters");
        Click(Items(control).First(), Mouse.MouseUpEvent);
        Check(executed.SequenceEqual(new[] { "CTP_Duong_Do" }), "reused row executes callback exactly once");

        var categories = Field<StackPanel>(control, "_categoriesPanel");
        Click((Border)((StackPanel)categories.Children[0]).Children[0]);
        Check(!VisibleCommands(control).Contains("CTP_Duong_Do"), "collapse category");
        Search(control, "duong do");
        Check(VisibleCommands(control).SequenceEqual(new[] { "CTP_Duong_Do" }), "search expands collapsed match");
        Search(control, "");
        Check(!VisibleCommands(control).Contains("CTP_Duong_Do"), "clear search restores collapse state");
        var collapsedItems = (StackPanel)((StackPanel)categories.Children[0]).Children[1];
        Check(collapsedItems.Children.Count == 0, "collapsed category creates no rows");
        Click((Border)((StackPanel)categories.Children[0]).Children[0]);
        Check(VisibleCommands(control).Count == 3, "expand lazily restores rows");

        commands[0].Label = "Nhãn mới";
        commands[0].Command = "CTP_New";
        commands[0].Description = "Nội dung mới";
        control.LoadCommands(commands);
        AssertSearch(control, "nhan moi", "CTP_New");
        AssertSearch(control, "duong do");
        Search(control, "");
        Check(!ReferenceEquals(firstItem, Items(control).First()), "reload invalidates row cache");
        Click(Items(control).First(), Mouse.MouseUpEvent);
        Check(executed.Last() == "CTP_New" && executed.Count == 2, "reload uses updated callback data");

        control.LoadCommands(new List<PaletteCommandInfo> { commands[0], commands[0] });
        Check(VisibleCommands(control).SequenceEqual(new[] { "CTP_New", "CTP_New" }), "duplicate object entries do not share a WPF parent");
        Search(control, "nhan");
        Check(VisibleCommands(control).Count == 2, "duplicate entries survive repeated filtering");
        control.LoadCommands(new List<PaletteCommandInfo>());
        Check(Items(control).Count == 0, "empty reload removes old rows");
        Check(Field<TextBlock>(control, "_statusText").Text.Contains("0 lệnh"), "empty status count");

        Console.WriteLine($"PASS: {_checks} palette regression checks (real WPF controls, no AutoCAD host).");
    }

    private static void AssertSearch(ToolPaletteControl control, string query, params string[] expected)
    {
        Search(control, query);
        Check(VisibleCommands(control).SequenceEqual(expected), $"search '{query}'");
    }

    private static void Search(ToolPaletteControl control, string text)
    {
        var box = Field<TextBox>(control, "_searchBox");
        box.Tag = null;
        box.Text = text;
    }

    private static List<Border> Items(ToolPaletteControl control)
    {
        return Field<StackPanel>(control, "_categoriesPanel").Children.Cast<StackPanel>()
            .Select(section => (StackPanel)section.Children[1])
            .Where(panel => panel.Visibility == Visibility.Visible)
            .SelectMany(panel => panel.Children.Cast<Border>()).ToList();
    }

    private static List<string> VisibleCommands(ToolPaletteControl control)
    {
        return Items(control).Select(item => ((Grid)item.Child).Children.OfType<TextBlock>()
            .Single(text => Grid.GetColumn(text) == 2).Text).ToList();
    }

    private static void Click(UIElement element, RoutedEvent? routedEvent = null)
    {
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = routedEvent ?? Mouse.MouseDownEvent
        });
    }

    private static T Field<T>(object obj, string name) =>
        (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;

    private static void Check(bool success, string label)
    {
        if (!success) throw new InvalidOperationException("FAIL: " + label);
        _checks++;
    }

    private static void Benchmark()
    {
        var control = new ToolPaletteControl(_ => { });
        var commands = ToolPaletteDataReader.GetDefaultCommands();
        control.LoadCommands(commands);
        Console.WriteLine($"Dataset: {commands.Count} built-in commands; Release; STA WPF; median of 5 runs.");
        Measure("search/no matches", 200, i => Search(control, "__missing_" + i));
        Measure("search/render matches", 40, i => Search(control, i % 2 == 0 ? "" : "CT"));
    }

    private static void Snapshot(string path)
    {
        var control = new ToolPaletteControl(_ => { });
        control.LoadCommands(ToolPaletteDataReader.GetDefaultCommands());
        // Exercise the same rows through filtering before checking their final appearance.
        Search(control, "CT");
        Search(control, "");
        var size = new Size(720, 920);
        control.Measure(size);
        control.Arrange(new Rect(size));
        control.UpdateLayout();
        var bitmap = new RenderTargetBitmap(720, 920, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
        var pixels = new byte[720 * 920 * 4];
        bitmap.CopyPixels(pixels, 720 * 4, 0);
        Console.WriteLine("Rendered pixels SHA256: " + Convert.ToHexString(SHA256.HashData(pixels)));
    }

    private static void Measure(string name, int iterations, Action<int> action)
    {
        for (int i = 0; i < 6; i++) action(i);
        var timings = new List<double>();
        var allocations = new List<long>();
        for (int sample = 0; sample < 5; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action(i);
            watch.Stop();
            allocations.Add(GC.GetAllocatedBytesForCurrentThread() - bytesBefore);
            timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        timings.Sort();
        allocations.Sort();
        Console.WriteLine($"{name}: {timings[2] / iterations:F3} ms/filter, {allocations[2] / iterations:N0} bytes/filter ({iterations} filters/run)");
    }
}

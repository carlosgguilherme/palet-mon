using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PaletMon.Logic;

namespace PaletMon.UI;

// Small builders so the tabs can be written as plain C# instead of XAML.
public static class Ui
{
    public static readonly FontFamily Display = new("Bungee, Arial Black, Segoe UI Black, Impact, sans-serif");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Menlo, monospace");

    public static T Res<T>(this T c, AvaloniaProperty prop, string key) where T : Control
    {
        c.Bind(prop, c.GetResourceObservable(key));
        return c;
    }

    public static TextBlock Text(string text, double size = 14, FontWeight? weight = null, string color = "Fg", bool wrap = true)
    {
        var t = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeight.Normal, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        return t.Res(TextBlock.ForegroundProperty, color);
    }
    public static TextBlock Muted(string text, double size = 13) => Text(text, size, null, "Muted");
    public static TextBlock H2(string text) => Text(text, 19, FontWeight.Normal).With(t => t.FontFamily = Display);
    public static TextBlock H3(string text) => Text(text, 15, FontWeight.ExtraBold);
    public static TextBlock Label(string text) => Text(text.ToUpperInvariant(), 11, FontWeight.ExtraBold, "Muted").With(t => t.LetterSpacing = 0.8);

    // **bold** markup → runs
    public static TextBlock Rich(string markup, double size = 14, string color = "Fg")
    {
        var t = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap }.Res(TextBlock.ForegroundProperty, color);
        var parts = markup.Split("**");
        t.Inlines = new InlineCollection();
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            t.Inlines.Add(new Run(parts[i]) { FontWeight = i % 2 == 1 ? FontWeight.Bold : FontWeight.Normal });
        }
        return t;
    }

    public static T With<T>(this T c, Action<T> f) { f(c); return c; }

    public static StackPanel Col(double gap, params Control[] children)
    {
        var s = new StackPanel { Spacing = gap };
        foreach (var c in children) if (c != null) s.Children.Add(c);
        return s;
    }
    public static StackPanel Row(double gap, params Control[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
        foreach (var c in children) if (c != null) s.Children.Add(c);
        return s;
    }
    public static WrapPanel Wrap(double gap, IEnumerable<Control> children)
    {
        var w = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var c in children) { c.Margin = new Thickness(0, 0, gap, gap); w.Children.Add(c); }
        return w;
    }

    public static Border Card(Control content, double pad = 18, string bg = "Panel")
    {
        return new Border
        {
            Child = content, Padding = new Thickness(pad), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
        }.Res(Border.BackgroundProperty, bg).Res(Border.BorderBrushProperty, "Line");
    }

    public static Grid PanelHead(Control left, Control right)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        g.Children.Add(left);
        if (right != null) { Grid.SetColumn(right, 1); right.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(right); }
        return g;
    }

    public static Color Hex(string hex) => Color.Parse(hex);
    public static Color Alpha(Color c, double a) => Color.FromArgb((byte)(a * 255), c.R, c.G, c.B);

    public static Border Chip(string type)
    {
        var dark = TypeChart.DarkText.Contains(type);
        return new Border
        {
            Background = new SolidColorBrush(Hex(TypeChart.Color[type])), CornerRadius = new CornerRadius(99), Padding = new Thickness(8, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = TypeChart.Pt[type].ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.ExtraBold, LetterSpacing = 0.5,
                Foreground = dark ? new SolidColorBrush(Hex("#1b1b1b")) : Brushes.White,
            },
        };
    }
    public static StackPanel Chips(IEnumerable<string> types) => Row(4, types.Select(t => (Control)Chip(t)).ToArray());

    public static Border Pill(string text, string kind)
    {
        var (bg, fg) = kind switch { "good" => ("GoodBg", "Good"), "warn" => ("WarnBg", "Warn"), "bad" => ("BadBg", "Bad"), "accent" => ("AccentSoft", "AccentInk"), _ => ("Panel2", "Muted") };
        return new Border { CornerRadius = new CornerRadius(99), Padding = new Thickness(10, 2), VerticalAlignment = VerticalAlignment.Center, Child = Text(text, 12.5, FontWeight.Bold, fg, false) }
            .Res(Border.BackgroundProperty, bg);
    }

    public static Border AlertBox(Alert a)
    {
        var (bg, edge) = a.Kind switch { "good" => ("GoodBg", "Good"), "bad" => ("BadBg", "Bad"), "neutral" => ("Panel2", "Muted"), _ => ("WarnBg", "Warn") };
        return new Border { Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(4, 0, 0, 0), Child = Rich(a.Text, 13.5) }
            .Res(Border.BackgroundProperty, bg).Res(Border.BorderBrushProperty, edge);
    }

    public static Button Btn(string text, Action onClick, bool primary = true, bool small = false)
    {
        var b = new Button
        {
            Content = text, FontWeight = FontWeight.ExtraBold, Padding = small ? new Thickness(10, 4) : new Thickness(16, 8),
            CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        if (primary) b.Res(Button.BackgroundProperty, "Accent").Res(Button.ForegroundProperty, "AccentFg").Res(Button.BorderBrushProperty, "Accent");
        else b.Res(Button.BackgroundProperty, "Panel").Res(Button.ForegroundProperty, "AccentInk").Res(Button.BorderBrushProperty, "AccentInk");
        b.Click += (_, _) => onClick();
        return b;
    }

    public static ComboBox Combo(IEnumerable<(string key, string label)> items, string selected, Action<string> onChange, double width = double.NaN)
    {
        var list = items.ToList();
        var cb = new ComboBox { Width = width, MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (key, label) in list) cb.Items.Add(new ComboBoxItem { Content = label, Tag = key });
        cb.SelectedIndex = Math.Max(0, list.FindIndex(i => i.key == selected));
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem it) onChange((string)it.Tag); };
        return cb;
    }

    public static CheckBox Check(string text, bool value, Action<bool> onChange)
    {
        var c = new CheckBox { Content = text, IsChecked = value };
        c.IsCheckedChanged += (_, _) => onChange(c.IsChecked == true);
        return c;
    }

    public static Control Field(string label, Control input) => Col(4, Label(label), input);

    public static AutoCompleteBox Search(string watermark)
    {
        return new AutoCompleteBox
        {
            Watermark = watermark, MinWidth = 220, FilterMode = AutoCompleteFilterMode.Contains, MaxDropDownHeight = 320,
            ItemsSource = Data.Dex.All.Select(m => m.Name).Distinct().OrderBy(n => n).ToList(),
        };
    }

    public static Border Badge(Data.Mon p)
    {
        var t = p.Types;
        var initials = string.Concat(p.Name.Split(' ').Where(w => w.Length > 0).Select(w => w[0])).ToUpperInvariant();
        if (initials.Length > 2) initials = initials[..2];
        return new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(10),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Hex(TypeChart.Color[t[0]]), 0), new GradientStop(Hex(TypeChart.Color[t.Length > 1 ? t[1] : t[0]]), 1) },
            },
            Child = new TextBlock { Text = initials, FontFamily = Display, FontSize = 18, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
    }

    public static Border MoveCard(PMove mv)
    {
        var c = Hex(TypeChart.Color[mv.Type]);
        var cls = mv.Info.Class == 2 ? "Físico" : mv.Info.Class == 3 ? "Especial" : "Status";
        var pw = mv.Info.Class == 1 ? "" : mv.Info.Power == -1 ? " · var." : $" · {mv.Info.Power}";
        return new Border
        {
            CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Margin = new Thickness(0, 0, 6, 6), BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Alpha(c, 0.16)), BorderBrush = new SolidColorBrush(Alpha(c, 0.5)),
            Child = Col(1, Text(mv.Name, 13.5, FontWeight.ExtraBold), Text($"{TypeChart.Pt[mv.Type]} · {cls}{pw} · {mv.Src}", 11, null, "Muted").With(t => t.FontFamily = Mono)),
        };
    }

    public static Grid StatBars(Data.Mon p)
    {
        string[] labels = { "HP", "ATQ", "DEF", "AT.E", "DF.E", "VEL" };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("40,34,*"), RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", 6))) };
        for (int i = 0; i < 6; i++)
        {
            int v = p.Stats[i];
            var k = Text(labels[i], 11.5, FontWeight.ExtraBold, "Muted", false);
            var val = Text(v.ToString(), 12, null, "Fg", false).With(t => { t.FontFamily = Mono; t.HorizontalAlignment = HorizontalAlignment.Right; t.Margin = new Thickness(0, 0, 8, 0); });
            var color = v >= 100 ? "Good" : v >= 70 ? "Accent" : v >= 50 ? "Warn" : "Bad";
            var frac = Math.Min(1.0, v / 160.0);
            var track = new Grid { ColumnDefinitions = new ColumnDefinitions($"{frac.ToString(System.Globalization.CultureInfo.InvariantCulture)}*,{(1 - frac + 0.0001).ToString(System.Globalization.CultureInfo.InvariantCulture)}*"), Height = 7, VerticalAlignment = VerticalAlignment.Center };
            var bg = new Border { CornerRadius = new CornerRadius(4) }.Res(Border.BackgroundProperty, "Panel2");
            Grid.SetColumnSpan(bg, 2);
            track.Children.Add(bg);
            track.Children.Add(new Border { CornerRadius = new CornerRadius(4) }.Res(Border.BackgroundProperty, color));
            foreach (var (c, col) in new (Control, int)[] { (k, 0), (val, 1), (track, 2) }) { Grid.SetRow(c, i); Grid.SetColumn(c, col); c.Margin = new Thickness(c.Margin.Left, 1.5, c.Margin.Right, 1.5); g.Children.Add(c); }
        }
        return g;
    }
}

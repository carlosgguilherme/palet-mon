using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaletMon.Data;
using PaletMon.Logic;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

// Touch-sized building blocks for the phone screens.
public static class Kit
{
    static readonly BoxShadows Soft = BoxShadows.Parse("0 2 10 0 #22000000");

    // Material icons (Apache 2.0), 24x24.
    public static class Icons
    {
        public const string Team = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5s-3 1.34-3 3 1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z";
        public const string Build = "M11.99 18.54l-7.37-5.73L3 14.07l9 7 9-7-1.63-1.27-7.38 5.74zM12 16l7.36-5.73L21 9l-9-7-9 7 1.63 1.27L12 16z";
        public const string Spark = "M19 9l1.25-2.75L23 5l-2.75-1.25L19 1l-1.25 2.75L15 5l2.75 1.25L19 9zm-7.5.5L9 4 6.5 9.5 1 12l5.5 2.5L9 20l2.5-5.5L17 12l-5.5-2.5zM19 15l-1.25 2.75L15 19l2.75 1.25L19 23l1.25-2.75L23 19l-2.75-1.25L19 15z";
        public const string Bolt = "M11 21h-1l1-7H7.5c-.58 0-.57-.32-.38-.66.19-.34.05-.08.07-.12C8.48 10.94 10.42 7.54 13 3h1l-1 7h3.5c.49 0 .56.33.47.51l-.07.15C12.96 17.55 11 21 11 21z";
        public const string Info = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
        public const string Down = "M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41z";
        public const string Right = "M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z";
        public const string Plus = "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z";
    }

    public static T Tap<T>(this T c, Action action) where T : Control
    {
        c.Tapped += (_, e) => { action(); e.Handled = true; };
        c.Cursor = new Cursor(StandardCursorType.Hand);
        return c;
    }

    public static Control Icon(string data, double size, string color)
    {
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Width = 24, Height = 24 }.Res(Shape.FillProperty, color);
        return new Viewbox { Width = size, Height = size, Child = path };
    }

    public static Border Surface(Control child, double pad = 16, string bg = "Panel") =>
        new Border { Child = child, Padding = new Thickness(pad), CornerRadius = new CornerRadius(18), BoxShadow = Soft }.Res(Border.BackgroundProperty, bg);

    public static Grid Columns(double gap, params Control[] items)
    {
        var defs = string.Join($",{gap.ToString(System.Globalization.CultureInfo.InvariantCulture)},", items.Select(_ => "*"));
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions(defs) };
        for (int i = 0; i < items.Length; i++) { Grid.SetColumn(items[i], i * 2); g.Children.Add(items[i]); }
        return g;
    }

    public static Control Tile(string value, string label, string kind)
    {
        var (bg, fg) = kind switch { "good" => ("GoodBg", "Good"), "warn" => ("WarnBg", "Warn"), "bad" => ("BadBg", "Bad"), _ => ("Panel", "Fg") };
        return new Border
        {
            CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 10), BoxShadow = Soft,
            Child = Col(0, Text(value, 22, FontWeight.Normal, fg, false).With(t => t.FontFamily = Display), Text(label, 11.5, FontWeight.Bold, "Muted")),
        }.Res(Border.BackgroundProperty, bg);
    }

    public static Control Segmented(IEnumerable<(string key, string label)> options, string selected, Action<string> pick)
    {
        var grid = new UniformGrid { Rows = 1 };
        foreach (var (key, label) in options)
        {
            bool on = key == selected;
            var item = new Border
            {
                CornerRadius = new CornerRadius(11), Padding = new Thickness(4, 10), Background = Brushes.Transparent,
                Child = Text(label, 13, FontWeight.Bold, on ? "AccentFg" : "Muted", false).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; }),
            };
            if (on) item.Res(Border.BackgroundProperty, "Accent");
            grid.Children.Add(item.Tap(() => pick(key)));
        }
        return new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(3), Child = grid }.Res(Border.BackgroundProperty, "Panel2");
    }

    public static Control Switch(bool on)
    {
        var knob = new Ellipse { Width = 20, Height = 20, Fill = Brushes.White, HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0) };
        return new Border { Width = 46, Height = 26, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Center, Child = knob }.Res(Border.BackgroundProperty, on ? "Accent" : "Line");
    }

    public static Control ToggleRow(string title, string desc, bool on, Action<bool> changed)
    {
        var text = Col(1, Text(title, 14.5, FontWeight.Bold));
        if (!string.IsNullOrEmpty(desc)) text.Children.Add(Muted(desc, 12.5));
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Background = Brushes.Transparent };
        var sw = Switch(on);
        Grid.SetColumn(sw, 2);
        g.Children.Add(text); g.Children.Add(sw);
        return new Border { Padding = new Thickness(0, 8), Background = Brushes.Transparent, Child = g }.Tap(() => changed(!on));
    }

    public static Control SelectRow(string label, string value, Action open)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        g.Children.Add(Col(1, Label(label), Text(value, 14.5, FontWeight.Bold)));
        var chev = Icon(Icons.Down, 22, "Muted");
        chev.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chev, 1);
        g.Children.Add(chev);
        return new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 9), Child = g }.Res(Border.BackgroundProperty, "Panel2").Tap(open);
    }

    public static Button Big(string text, Action action, bool primary = true)
    {
        var b = Btn(text, action, primary);
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.HorizontalContentAlignment = HorizontalAlignment.Center;
        b.Padding = new Thickness(16, 13);
        b.CornerRadius = new CornerRadius(14);
        b.FontSize = 15;
        return b;
    }

    public static Color Shade(Color c, double f) => Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));

    public static IBrush TypeGradient(string[] types)
    {
        var a = Hex(TypeChart.Color[types[0]]);
        var b = Hex(TypeChart.Color[types.Length > 1 ? types[1] : types[0]]);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Shade(a, 0.86), 0), new GradientStop(Shade(b, 0.62), 1) },
        };
    }

    public static Border GlassChip(string text) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(56, 255, 255, 255)), CornerRadius = new CornerRadius(99), Padding = new Thickness(9, 2),
        Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
    };

    public static Control Initials(Mon p, double size)
    {
        var text = string.Concat(p.Name.Split(' ').Where(w => w.Length > 0).Select(w => w[0])).ToUpperInvariant();
        return new TextBlock { Text = text.Length > 2 ? text[..2] : text, FontFamily = Display, FontSize = size, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    // Round portrait in the Pokémon's type colors.
    public static Control Portrait(Mon p, double size, bool art = true) => new Border
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = TypeGradient(p.Types),
        Child = Sprites.View(p, size, art, Initials(p, size * 0.36)),
    };

    public static Control TypeButton(string type, bool on, Action tap)
    {
        var c = Hex(TypeChart.Color[type]);
        var dark = TypeChart.DarkText.Contains(type);
        return new Border
        {
            CornerRadius = new CornerRadius(99), Padding = new Thickness(12, 7), BorderThickness = new Thickness(1.5), BorderBrush = new SolidColorBrush(Alpha(c, on ? 1 : 0.55)),
            Background = on ? new SolidColorBrush(c) : new SolidColorBrush(Alpha(c, 0.14)),
            Child = new TextBlock { Text = TypeChart.Pt[type], FontSize = 13, FontWeight = FontWeight.Bold }.With(t => { if (on) t.Foreground = dark ? new SolidColorBrush(Hex("#1b1b1b")) : Brushes.White; else t.Res(TextBlock.ForegroundProperty, "Fg"); }),
        }.Tap(tap);
    }

    public static Control SectionTitle(string text, string note = null)
    {
        var col = Col(1, H2(text).With(t => t.FontSize = 17));
        if (note != null) col.Children.Add(Muted(note, 12.5));
        col.Margin = new Thickness(2, 6, 2, 0);
        return col;
    }

    public static Control Stepper(int value, Action<int> changed)
    {
        int Clamp(int v) => Math.Max(1, Math.Min(100, v));
        var box = new TextBox
        {
            Text = value.ToString(), Width = 58, MinHeight = 40, Padding = new Thickness(2, 6), TextAlignment = TextAlignment.Center, FontFamily = Mono, FontSize = 16,
            VerticalContentAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(10),
        };
        Avalonia.Input.TextInput.TextInputOptions.SetContentType(box, Avalonia.Input.TextInput.TextInputContentType.Digits);
        void Commit() { if (int.TryParse(box.Text, out var v) && Clamp(v) != value) changed(Clamp(v)); else box.Text = value.ToString(); }
        box.LostFocus += (_, _) => Commit();
        box.KeyUp += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        Control Step(string text, int delta) => new Border
        {
            Width = 44, Height = 40, CornerRadius = new CornerRadius(10),
            Child = Text(text, 14, FontWeight.ExtraBold, "Fg", false).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }),
        }.Res(Border.BackgroundProperty, "Panel2").Tap(() => { var v = Clamp(value + delta); if (v != value) changed(v); });
        return Row(6, Step("−10", -10), Step("−", -1), box, Step("+", 1), Step("+10", 10));
    }
}

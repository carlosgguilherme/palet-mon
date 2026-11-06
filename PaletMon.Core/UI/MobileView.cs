using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PaletMon.Data;
using PaletMon.Logic;
using static PaletMon.UI.Kit;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

// Phone shell: app bar, bottom navigation and sheets. Same engine and features as MainView, laid out for touch.
public sealed partial class MobileView : UserControl
{
    static AppState S => Engine.S;

    static readonly (string key, string label, string icon)[] Nav =
    {
        ("time", "Meu time", Icons.Team), ("montar", "Montar", Icons.Build), ("gerador", "Gerador", Icons.Spark),
        ("batalha", "Batalha", Icons.Bolt), ("sobre", "Sobre", Icons.Info),
    };

    readonly StackPanel page = new() { Spacing = 12 };
    readonly ScrollViewer scroller;
    readonly Grid navBar = new();
    readonly Grid overlay = new() { IsVisible = false };
    readonly TextBlock modeChip;
    readonly UpdateFlow flow = new();
    Button updateBtn;
    ProgressBar progressBar;
    bool sheetLocked;
    Control scrollTarget;

    public MobileView()
    {
        this.Res(BackgroundProperty, "Bg");
        FontFamily = new FontFamily("fonts:Inter#Inter"); // the phone's default font ignores font weights
        modeChip = new TextBlock { FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };

        scroller = new ScrollViewer { Content = new Border { Padding = new Thickness(14, 14, 14, 24), Child = page } };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var bar = AppBar();
        Grid.SetRow(scroller, 1);
        var nav = new Border { Child = navBar, Padding = new Thickness(4, 8, 4, 8), BoxShadow = BoxShadows.Parse("0 -2 10 0 #22000000") }.Res(Border.BackgroundProperty, "Panel");
        Grid.SetRow(nav, 2);
        Grid.SetRowSpan(overlay, 3);
        root.Children.Add(bar); root.Children.Add(scroller); root.Children.Add(nav); root.Children.Add(overlay);
        Content = root;

        WireUpdates();
        Render();
        Loaded += (_, _) =>
        {
            var top = TopLevel.GetTopLevel(this);
            if (top != null) top.BackRequested += (_, e) => { if (overlay.IsVisible && !sheetLocked) { CloseSheet(); e.Handled = true; } };
            flow.AutoCheck();
        };
    }

    Control AppBar()
    {
        var brand = Row(10, Brand.Logo(40), Brand.Wordmark(19, "White"));
        brand.VerticalAlignment = VerticalAlignment.Center;
        var chevron = Icon(Icons.Down, 18, "White");
        var chip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), CornerRadius = new CornerRadius(99), Padding = new Thickness(12, 7, 8, 7),
            VerticalAlignment = VerticalAlignment.Center, Child = Row(3, modeChip, chevron),
        }.Tap(OpenModeSheet);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(chip, 1);
        row.Children.Add(brand); row.Children.Add(chip);
        return Col(0, new Border { Padding = new Thickness(14, 10), Child = row }.Res(Border.BackgroundProperty, "Ink"), new HazardStripe { Height = 5, Rounded = false });
    }

    void Save() => S.Save();

    // Entry points for the screenshot tool.
    public void Expand(int i) { expanded = i; Render(); }
    public void Section(string name) { teamSection = name; Render(); }
    public void GenerateNow() { Generate(); Render(); }
    public void ShowMode() => OpenModeSheet();
    public void ShowSearch() => PickMon("Adicionar ao time", _ => { });
    public void ShowUpdatePrompt() => ShowNotice(new UpdateNotice("good", "Nova versão disponível: **2.3.0**. Para atualizar você precisa estar com internet (62 MB).", new[] { new UpdateAction("Atualizar agora", () => { }), new UpdateAction("Agora não", () => { }, false) }));

    static string ModeShort()
    {
        var m = S.Mode;
        return $"{Engine.FormatLabel[m.Format]} {(m.Pick < m.Size ? $"{m.Pick} de {m.Size}" : $"{m.Size}x{m.Size}")}";
    }

    void Go(string tab)
    {
        S.Tab = tab; Save(); Render();
        scroller.Offset = new Vector(0, 0);
    }

    public void Render()
    {
        modeChip.Text = ModeShort();
        RenderNav();
        page.Children.Clear();
        updateBtn = null;
        scrollTarget = null;
        page.Children.Add(S.Tab switch
        {
            "montar" => MontarPage(),
            "gerador" => GeradorPage(),
            "batalha" => BatalhaPage(),
            "sobre" => SobrePage(),
            _ => TimePage(),
        });
        if (scrollTarget != null)
        {
            var target = scrollTarget;
            Dispatcher.UIThread.Post(() => target.BringIntoView(), DispatcherPriority.Background);
        }
    }

    void RenderNav()
    {
        navBar.Children.Clear();
        navBar.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Nav.Select(_ => "*")));
        for (int i = 0; i < Nav.Length; i++)
        {
            var (key, label, icon) = Nav[i];
            bool on = S.Tab == key;
            var pill = new Border { Width = 58, Height = 30, CornerRadius = new CornerRadius(15), HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent, Child = Icon(icon, 22, on ? "AccentFg" : "Muted").With(c => { c.HorizontalAlignment = HorizontalAlignment.Center; c.VerticalAlignment = VerticalAlignment.Center; }) };
            if (on) pill.Res(Border.BackgroundProperty, "Accent");
            var item = new Border
            {
                Background = Brushes.Transparent,
                Child = Col(3, pill, Text(label, 11.5, on ? FontWeight.ExtraBold : FontWeight.SemiBold, on ? "Fg" : "Muted", false).With(t => t.HorizontalAlignment = HorizontalAlignment.Center)),
            }.Tap(() => Go(key));
            Grid.SetColumn(item, i);
            navBar.Children.Add(item);
        }
    }

    // Runs heavy work off the UI thread, then re-renders.
    void RunBusy(Button b, string busy, Action work, bool scrollToResult = true)
    {
        b.IsEnabled = false; b.Content = busy;
        Task.Run(work).ContinueWith(_ => Dispatcher.UIThread.Post(() => { wantResult = scrollToResult; Render(); wantResult = false; }));
    }
    bool wantResult;

    // ---------- sheets ----------
    void OpenSheet(string title, Control body, bool tall = false, bool locked = false)
    {
        sheetLocked = locked;
        overlay.Children.Clear();
        var scrim = new Border { Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)) };
        if (!locked) scrim.Tap(CloseSheet);
        var handle = new Border { Width = 42, Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center }.Res(Border.BackgroundProperty, "Line");
        var height = Bounds.Height > 0 ? Bounds.Height : 800;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var heading = H2(title).With(t => { t.FontSize = 17; t.Margin = new Thickness(0, 12, 0, 10); });
        var scroll = new ScrollViewer { Content = body };
        Grid.SetRow(heading, 1); Grid.SetRow(scroll, 2);
        grid.Children.Add(handle); grid.Children.Add(heading); grid.Children.Add(scroll);
        var sheet = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(22, 22, 0, 0), Padding = new Thickness(16, 10, 16, 18),
            MaxHeight = height * 0.86, Child = grid,
        }.Res(Border.BackgroundProperty, "Panel");
        if (tall) sheet.Height = height * 0.86;
        overlay.Children.Add(scrim); overlay.Children.Add(sheet);
        overlay.IsVisible = true;
    }

    void CloseSheet() { overlay.IsVisible = false; overlay.Children.Clear(); sheetLocked = false; }

    void PickSheet(string title, IEnumerable<(string key, string label)> options, string selected, Action<string> pick)
    {
        var list = Col(2);
        foreach (var (key, label) in options)
        {
            bool on = key == selected;
            var row = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12), Background = Brushes.Transparent, Child = Text(label, 15, on ? FontWeight.ExtraBold : FontWeight.Normal, on ? "AccentInk" : "Fg") };
            if (on) row.Res(Border.BackgroundProperty, "AccentSoft");
            list.Children.Add(row.Tap(() => { CloseSheet(); pick(key); }));
        }
        OpenSheet(title, list);
    }

    void PickMon(string title, Action<Mon> pick)
    {
        var box = new TextBox { Watermark = "Nome do Pokémon", MinHeight = 44, CornerRadius = new CornerRadius(12), VerticalContentAlignment = VerticalAlignment.Center };
        var list = Col(2);
        void Fill()
        {
            list.Children.Clear();
            var q = (box.Text ?? "").Trim();
            var found = Dex.All.Where(m => m.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1).Take(30);
            foreach (var m in found)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*") };
                var info = Col(3, Text(m.Name, 15, FontWeight.Bold), Chips(m.Types));
                Grid.SetColumn(info, 2);
                g.Children.Add(Portrait(m, 46, false)); g.Children.Add(info);
                list.Children.Add(new Border { Padding = new Thickness(4, 7), CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, Child = g }.Tap(() => { CloseSheet(); pick(m); }));
            }
            if (list.Children.Count == 0) list.Children.Add(Muted("Nenhum Pokémon com esse nome.", 14));
        }
        box.TextChanged += (_, _) => Fill();
        Fill();
        OpenSheet(title, Col(10, box, list), tall: true);
    }

    // ---------- battle mode ----------
    void ModeChanged()
    {
        lastBuild = null; genResult = null; genEmpty = false; buildShown.Clear(); genShown.Clear();
        Save(); Render(); OpenModeSheet();
    }

    void Custom(Action set)
    {
        set();
        S.Mode.Pick = Math.Min(S.Mode.Pick, S.Mode.Size);
        S.Mode.Preset = "custom";
        ModeChanged();
    }

    void OpenModeSheet()
    {
        var m = S.Mode;
        var presets = Engine.Presets.Select(p => (p.Key, p.Value.Label)).Append(("custom", "Personalizado")).ToList();
        var sizes = new[] { 6, 5, 4, 3, 2, 1 };
        var picks = sizes.Where(n => n <= m.Size).Select(n => (n.ToString(), n == m.Size ? $"Todos ({n})" : $"{n} de {m.Size}")).ToList();
        var body = Col(10,
            Muted(Engine.ModeSummary(), 13),
            SelectRow("Regra pronta", presets.First(p => p.Item1 == m.Preset).Item2, () => PickSheet("Regra pronta", presets, m.Preset, k =>
            {
                if (k == "custom") S.Mode.Preset = "custom"; else Engine.ApplyPreset(k);
                ModeChanged();
            })),
            Col(4, Label("Formato"), Segmented(Engine.FormatLabel.Select(x => (x.Key, x.Value)), m.Format, k => Custom(() => S.Mode.Format = k))),
            Columns(8,
                SelectRow("No time", m.Size.ToString(), () => PickSheet("Pokémon no time", sizes.Select(n => (n.ToString(), n.ToString())), m.Size.ToString(), k =>
                {
                    bool all = S.Mode.Pick == S.Mode.Size;
                    Custom(() => { S.Mode.Size = int.Parse(k); if (all) S.Mode.Pick = S.Mode.Size; });
                })),
                SelectRow("Leva", picks.First(p => p.Item1 == m.Pick.ToString()).Item2, () => PickSheet("Leva para a luta", picks, m.Pick.ToString(), k => Custom(() => S.Mode.Pick = int.Parse(k))))),
            SelectRow("Nível", Engine.LevelLabel[m.Level], () => PickSheet("Nível", Engine.LevelLabel.Select(x => (x.Key, x.Value)), m.Level, k => Custom(() => S.Mode.Level = k))),
            ToggleRow("Proibir lendários e míticos", null, m.BanLegends, v => Custom(() => S.Mode.BanLegends = v)),
            ToggleRow("Cláusula de sono", "Tira Spore, Hypnosis e parecidos das sugestões", m.SleepClause, v => Custom(() => S.Mode.SleepClause = v)),
            Big("Pronto", CloseSheet));
        OpenSheet("Modo de combate", body);
    }

    // ---------- updates ----------
    void WireUpdates()
    {
        flow.OpenLink = url => OpenLink(this, url);
        flow.Changed += ShowNotice;
        flow.StateChanged += () =>
        {
            if (updateBtn == null) return;
            updateBtn.IsEnabled = !flow.Busy;
            updateBtn.Content = flow.ButtonLabel;
        };
        flow.ProgressChanged += p => Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = p; });
    }

    void ShowNotice(UpdateNotice n)
    {
        if (n == null) { CloseSheet(); return; }
        var body = Col(12, Rich(n.Text, 15));
        progressBar = null;
        if (n.Progress)
        {
            progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
            body.Children.Add(progressBar);
        }
        foreach (var a in n.Actions) body.Children.Add(Big(a.Label, a.Run, a.Primary));
        OpenSheet("Atualização", body, locked: n.Actions.Length == 0);
    }
}

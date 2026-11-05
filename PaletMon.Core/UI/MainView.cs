using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PaletMon.Logic;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

public sealed partial class MainView : UserControl
{
    static AppState S => Engine.S;
    readonly StackPanel content = new() { Spacing = 16 };
    readonly StackPanel tabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    readonly ScrollViewer scroller;

    static readonly (string key, string label)[] TabList = { ("time", "Meu time"), ("montar", "Montar time"), ("gerador", "Gerador"), ("batalha", "Batalha"), ("sobre", "Sobre") };

    public MainView()
    {
        this.Res(BackgroundProperty, "Bg");

        var brand = Row(14, Brand.Logo(72), Col(4, Brand.Wordmark(30), Muted("Seu time, bem empilhado. Fraquezas, cobertura, golpes e times para o Cobbleverse.", 13.5)));
        brand.VerticalAlignment = VerticalAlignment.Center;
        var tabBox = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(4), Child = tabs, VerticalAlignment = VerticalAlignment.Center }.Res(Border.BackgroundProperty, "Panel2");
        var header = new Border
        {
            Padding = new Thickness(24, 18, 24, 12),
            Child = Col(14, PanelHead(brand, tabBox), new HazardStripe(), updateBar),
        };
        DockPanel.SetDock(header, Dock.Top);

        scroller = new ScrollViewer { Content = new Border { Padding = new Thickness(24, 4, 24, 40), Child = content } };
        Content = new DockPanel { Children = { header, scroller } };
        WireUpdates();
        Render();
#if SELF_TEST
        // Test builds only: run the same path as the button.
        Loaded += (_, _) => flow.Check();
#else
        Loaded += (_, _) => flow.AutoCheck();
#endif
    }

    void Save() => S.Save();

    public void Render()
    {
        tabs.Children.Clear();
        foreach (var (key, label) in TabList)
        {
            var on = S.Tab == key;
            var b = new Button { Content = label, FontWeight = FontWeight.Bold, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(0) };
            if (on) b.Res(BackgroundProperty, "Accent").Res(ForegroundProperty, "AccentFg");
            else b.Res(ForegroundProperty, "Muted").With(x => x.Background = Brushes.Transparent);
            b.Click += (_, _) => { S.Tab = key; Save(); Render(); scroller.Offset = new Vector(0, 0); };
            tabs.Children.Add(b);
        }
        content.Children.Clear();
        updateBtn = null;
        if (S.Tab == "sobre") { content.Children.Add(SobreTab()); return; }
        content.Children.Add(ModeBar());
        content.Children.Add(S.Tab switch
        {
            "montar" => MontarTab(),
            "gerador" => GeradorTab(),
            "batalha" => BatalhaTab(),
            _ => TimeTab(),
        });
        content.Children.Add(Muted("Dados de tipos, atributos, habilidades, evoluções e golpes vêm da PokéAPI (golpes de Scarlet/Violet, ou do jogo mais recente em que o Pokémon aparece). O Cobblemon segue esses dados de perto, mas alguns métodos de evolução mudam no mod. Golpes marcados \"TM/tutor/ovo\" precisam de TM, tutor ou reprodução. Palet Mon é um projeto de fã, sem ligação com Nintendo, The Pokémon Company ou Cobblemon.", 12));
    }

    // Runs heavy work off the UI thread, then re-renders.
    void RunBusy(Button b, string busy, Action work)
    {
        b.IsEnabled = false; b.Content = busy;
        Task.Run(work).ContinueWith(_ => Dispatcher.UIThread.Post(Render));
    }

    // ---------- battle mode bar ----------
    Control ModeBar()
    {
        var m = S.Mode;
        void Changed() { lastBuild = null; genResult = null; genEmpty = false; buildShown.Clear(); genShown.Clear(); Save(); Render(); }
        void Custom(Action set)
        {
            set();
            S.Mode.Pick = Math.Min(S.Mode.Pick, S.Mode.Size);
            S.Mode.Preset = "custom";
            Changed();
        }
        var presets = Engine.Presets.Select(p => (p.Key, p.Value.Label)).Append(("custom", "Personalizado"));
        var grid = new WrapPanel();
        Control F(string label, Control c, double width = 200) { c.Width = width; var f = Field(label, c); f.Margin = new Thickness(0, 0, 14, 10); return f; }
        grid.Children.Add(F("Regra pronta", width: 310, c: Combo(presets, m.Preset, k => { if (k == "custom") { S.Mode.Preset = "custom"; Changed(); } else { Engine.ApplyPreset(k); Changed(); } })));
        grid.Children.Add(F("Formato", Combo(Engine.FormatLabel.Select(x => (x.Key, x.Value)), m.Format, k => Custom(() => S.Mode.Format = k))));
        grid.Children.Add(F("Pokémon no time", Combo(new[] { 6, 5, 4, 3, 2, 1 }.Select(n => (n.ToString(), n.ToString())), m.Size.ToString(), k =>
        {
            bool all = S.Mode.Pick == S.Mode.Size;
            Custom(() => { S.Mode.Size = int.Parse(k); if (all) S.Mode.Pick = S.Mode.Size; });
        })));
        grid.Children.Add(F("Leva para a luta", Combo(new[] { 6, 5, 4, 3, 2, 1 }.Where(n => n <= m.Size).Select(n => (n.ToString(), n == m.Size ? $"Todos ({n})" : $"{n} de {m.Size}")), m.Pick.ToString(), k => Custom(() => S.Mode.Pick = int.Parse(k)))));
        grid.Children.Add(F("Nível", Combo(Engine.LevelLabel.Select(x => (x.Key, x.Value)), m.Level, k => Custom(() => S.Mode.Level = k))));
        var checks = Col(6, Check("Proibir lendários e míticos", m.BanLegends, v => Custom(() => S.Mode.BanLegends = v)), Check("Cláusula de sono", m.SleepClause, v => Custom(() => S.Mode.SleepClause = v)));
        checks.VerticalAlignment = VerticalAlignment.Bottom;
        grid.Children.Add(checks);
        return Card(Col(12, PanelHead(H2("Modo de combate"), Pill(Engine.ModeSummary(), "neutral")), grid));
    }

    // ---------- checkbox tiles shared by Gerador and Montar ----------
    Control BoxGrid(BoxConfig cfg, IEnumerable<string> keys, Action changed)
    {
        var tiles = new List<Control>();
        foreach (var box in Engine.Boxes.Where(b => keys.Contains(b.Key)))
        {
            var cb = new CheckBox { IsChecked = cfg[box.Key], Content = Col(2, H3(box.Title), Muted(box.Desc, 12.5)), VerticalAlignment = VerticalAlignment.Top };
            cb.IsCheckedChanged += (_, _) =>
            {
                cfg[box.Key] = cb.IsChecked == true;
                if (box.Key == "wishOnly" && cfg[box.Key]) cfg["wishPrio"] = false;
                changed();
            };
            var inner = Col(8, cb);
            if (box.Extra == "weatherKind") inner.Children.Add(Combo(Engine.Weathers.Select(w => (w.Key, w.Value.Label)), cfg.WeatherKind, k => { cfg.WeatherKind = k; cfg["weather"] = true; changed(); }, 160).With(c => c.Margin = new Thickness(28, 0, 0, 0)));
            if (box.Extra == "monoType") inner.Children.Add(Combo(TypeChart.All.Select(t => (t, TypeChart.Pt[t])), cfg.MonoType, k => { cfg.MonoType = k; cfg["mono"] = true; changed(); }, 160).With(c => c.Margin = new Thickness(28, 0, 0, 0)));
            if (box.Extra == "genNum") inner.Children.Add(Combo(Enumerable.Range(1, 9).Select(n => (n.ToString(), $"Geração {n}")), cfg.GenNum.ToString(), k => { cfg.GenNum = int.Parse(k); cfg["gen"] = true; changed(); }, 160).With(c => c.Margin = new Thickness(28, 0, 0, 0)));
            var tile = new Border { Width = 262, Padding = new Thickness(12), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(cfg[box.Key] ? 2 : 1), Child = inner }
                .Res(Border.BackgroundProperty, cfg[box.Key] ? "AccentSoft" : "Bg").Res(Border.BorderBrushProperty, cfg[box.Key] ? "Accent" : "Line");
            tiles.Add(tile);
        }
        var col = Col(10, Wrap(10, tiles));
        if (cfg["fast"] && cfg["trickroom"]) col.Children.Add(AlertBox(new Alert("warn", "Rápido e Trick Room puxam em direções opostas. O resultado fica menos focado.")));
        return col;
    }

    // ---------- generated team cards ----------
    Control TeamResult(string title, GenResult r, bool best, IEnumerable<string> why, Control buttons, bool showReasons)
    {
        var cores = r.Team.Where(z => z.Core).ToList();
        var cards = r.Team.Select(z =>
        {
            var mv = Engine.PickMoves(z.P, null, S.Tms);
            var tag = z.Core ? Pill("escolhido", "good") : z.Kept ? Pill("seu", "neutral") : null;
            var col = Col(4,
                Row(6, Text(z.P.Name, 15, FontWeight.ExtraBold, "Fg", false), tag),
                Chips(z.P.Types),
                Rich($"Habilidade: **{z.Ab}**", 12.5),
                Muted($"{Engine.RoleOf(z.P).Name} · BST {z.P.Bst}", 12.5));
            if (showReasons && !z.Core && cores.Count > 0) col.Children.Add(Text(Engine.SupportReason(z, cores), 12.5));
            col.Children.Add(Muted(string.Join(" · ", mv.Select(m => m.Name)), 12));
            var card = Card(col, 10, "Panel");
            card.Width = 250;
            return (Control)card;
        });
        var sc = r.Sc ?? Engine.ScoreTeam(r.Team);
        var pills = Row(6, Pill($"STAB cobre {sc.Covered.Count}/18", sc.Covered.Count >= 14 ? "good" : "warn"),
            Pill(sc.Holes.Count > 0 ? "Brechas: " + string.Join(", ", sc.Holes.Select(t => TypeChart.Pt[t])) : "Sem brechas", sc.Holes.Count > 0 ? "warn" : "good"),
            Pill($"BST médio {Math.Round(sc.AvgBst)}", "neutral"));
        var body = Col(12, PanelHead(H3(title), pills), Wrap(8, cards));
        var whyList = why.ToList();
        if (whyList.Count > 0) body.Children.Add(Col(4, whyList.Select(w => (Control)Rich("•  " + w, 13.5)).ToArray()));
        if (buttons != null) body.Children.Add(buttons);
        var border = Card(body, 14, "Bg");
        if (best) { border.BorderThickness = new Thickness(2); border.Res(Border.BorderBrushProperty, "Accent"); }
        return border;
    }

    void UseAsTeam(List<Member> team)
    {
        int fallback = S.Team.Count > 0 ? S.Team.Max(m => m.Lv) : 50;
        S.Team = team.Select(z =>
        {
            var owned = S.Team.FirstOrDefault(m => Data.Dex.ById[m.Id].Chain == z.P.Chain);
            return owned != null ? new TeamSlot { Id = owned.Id, Lv = owned.Lv, Ab = owned.Ab } : new TeamSlot { Id = z.P.Id, Lv = fallback, Ab = z.Ab };
        }).ToList();
        S.Tab = "time"; Save(); Render(); scroller.Offset = new Vector(0, 0);
    }
}

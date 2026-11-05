using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PaletMon.Data;
using PaletMon.Logic;
using static PaletMon.UI.Kit;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

public sealed partial class MobileView
{
    string teamSection = "time";
    int expanded = -1;
    bool allAlerts;
    List<GenResult> lastBuild;
    GenResult genResult;
    bool genEmpty;
    readonly HashSet<string> buildShown = new();
    readonly HashSet<string> genShown = new();

    // ---------- Meu time ----------
    Control TimePage()
    {
        var members = Engine.ResolveTeam();
        foreach (var x in members) x.Moves = Engine.PickMoves(x.P, Engine.BattleLv(x.Lv), S.Tms);
        var sc = Engine.ScoreTeam(members);
        var offense = Engine.Offense(members);
        int covered = offense.Count(o => o.Best >= 2);

        var col = Col(12,
            Columns(8,
                Tile($"{covered}/18", "cobertura", covered >= 14 ? "good" : covered >= 10 ? "warn" : "bad"),
                Tile(sc.Holes.Count.ToString(), sc.Holes.Count == 1 ? "brecha" : "brechas", sc.Holes.Count == 0 ? "good" : sc.Holes.Count <= 2 ? "warn" : "bad"),
                Tile(Math.Round(sc.AvgBst).ToString(), "BST médio", "neutral")),
            Segmented(new[] { ("time", "Time"), ("defesa", "Defesa"), ("ataque", "Ataque") }, teamSection, k => { teamSection = k; Render(); }));

        if (teamSection == "defesa") col.Children.Add(DefenseSection(members, sc));
        else if (teamSection == "ataque") col.Children.Add(AttackSection(members, offense));
        else col.Children.Add(TeamSection(members, sc));
        return col;
    }

    Control TeamSection(List<Member> members, TeamScore sc)
    {
        var col = Col(12);
        var alerts = Engine.TeamAlerts(members, sc);
        var shown = allAlerts ? alerts : alerts.Take(2).ToList();
        foreach (var a in shown) col.Children.Add(AlertBox(a));
        if (alerts.Count > 2)
            col.Children.Add(Text(allAlerts ? "Mostrar menos avisos" : $"Ver mais {alerts.Count - 2} avisos", 13.5, FontWeight.Bold, "AccentInk").With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 2); }).Tap(() => { allAlerts = !allAlerts; Render(); }));

        for (int i = 0; i < members.Count; i++) col.Children.Add(MonCard(members[i], i));
        if (members.Count < 6)
        {
            var add = new Border
            {
                CornerRadius = new CornerRadius(18), Padding = new Thickness(14), BorderThickness = new Thickness(1.5), Background = Brushes.Transparent,
                Child = Row(8, Icon(Icons.Plus, 22, "AccentInk"), Text($"Adicionar Pokémon ({members.Count}/6)", 15, FontWeight.Bold, "AccentInk", false)).With(r => r.HorizontalAlignment = HorizontalAlignment.Center),
            }.Res(Border.BorderBrushProperty, "AccentInk").Tap(() => PickMon("Adicionar ao time", p =>
            {
                S.Team.Add(new TeamSlot { Id = p.Id, Lv = S.Team.Count > 0 ? S.Team.Max(m => m.Lv) : 5 });
                expanded = S.Team.Count - 1;
                Save(); Render();
            }));
            col.Children.Add(add);
        }
        col.Children.Add(Surface(ToggleRow("Golpes de TM, tutor e ovo", "Considerar nas sugestões de golpes", S.Tms, v => { S.Tms = v; Save(); Render(); }), 10));
        return col;
    }

    Control MonCard(Member x, int i)
    {
        bool open = expanded == i;
        bool ready = Engine.NextEvos(x.P).Any(k => k.Evo != null && k.Evo.Level > 0 && x.Lv >= k.Evo.Level);

        var info = Col(6,
            new TextBlock { Text = x.P.Name, FontFamily = Display, FontSize = 19, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis },
            Row(4, x.P.Types.Select(t => (Control)GlassChip(TypeChart.Pt[t])).ToArray()),
            Row(4, GlassChip($"Nv {Engine.BattleLv(x.Lv)}"), ready ? GlassChip("Pronto para evoluir") : null));
        info.VerticalAlignment = VerticalAlignment.Center;

        var halo = new Ellipse { Width = 92, Height = 92, Fill = new SolidColorBrush(Color.FromArgb(44, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var art = new Grid { Width = 96, Height = 96 };
        art.Children.Add(halo);
        art.Children.Add(Sprites.View(x.P, 90, true, Initials(x.P, 30)).With(s => { s.HorizontalAlignment = HorizontalAlignment.Center; s.VerticalAlignment = VerticalAlignment.Center; }));

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(art, 1);
        head.Children.Add(info); head.Children.Add(art);
        var header = new Border
        {
            Background = TypeGradient(x.P.Types), Padding = new Thickness(16, 10, 10, 10),
            CornerRadius = open ? new CornerRadius(18, 18, 0, 0) : new CornerRadius(18), Child = head,
        }.Tap(() => { expanded = open ? -1 : i; Render(); });

        var body = Col(0, header);
        if (open) body.Children.Add(new Border { Padding = new Thickness(14), Child = MonDetails(x, i) });
        return Surface(body, 0);
    }

    Control MonDetails(Member x, int i)
    {
        var role = Engine.RoleOf(x.P);
        var abilities = x.P.Abilities.Select(a => (a.TrimStart('*'), a.TrimStart('*') + (a.StartsWith("*") ? " (oculta)" : ""))).ToList();
        var moveLabel = $"Golpes sugeridos no nível {Engine.BattleLv(x.Lv)}" + (Engine.IsMulti ? $" · {Engine.FormatLabel[S.Mode.Format].ToLowerInvariant()}" : "");
        var moves = new UniformGrid { Columns = 2 };
        foreach (var m in x.Moves) moves.Children.Add(MoveCard(m));
        return Col(12,
            Col(6, Label("Nível"), Stepper(x.Lv, v => { S.Team[i].Lv = v; Save(); Render(); })),
            SelectRow("Habilidade", x.Ab, () => PickSheet("Habilidade", abilities, x.Ab, k => { S.Team[i].Ab = k; Save(); Render(); })),
            Col(3, Rich($"**{role.Name}**", 15), Muted(role.Tip, 13)),
            StatBars(x.P),
            MainView.EvoBlock(x.P, x.Lv),
            Col(6, Label(moveLabel), x.Moves.Count > 0 ? moves : Muted("Sem golpes de dano disponíveis ainda.")),
            Big("Remover do time", () => { S.Team.RemoveAt(i); expanded = -1; Save(); Render(); }, primary: false));
    }

    Control DefenseSection(List<Member> members, TeamScore sc)
    {
        if (members.Count == 0) return Surface(Muted("Adicione Pokémon para ver a tabela.", 14));
        int n = members.Count;
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto," + string.Join(",", Enumerable.Repeat("*", n + 2))),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", 19))),
        };
        void Put(Control c, int r, int col) { Grid.SetRow(c, r); Grid.SetColumn(c, col); c.Margin = new Thickness(1.5); g.Children.Add(c); }
        for (int i = 0; i < n; i++) Put(Portrait(members[i].P, 26, false).With(p => p.HorizontalAlignment = HorizontalAlignment.Center), 0, i + 1);
        Put(Text("Fr", 11, FontWeight.Bold, "Muted", false).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }), 0, n + 1);
        Put(Text("Res", 11, FontWeight.Bold, "Muted", false).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }), 0, n + 2);
        for (int r = 0; r < 18; r++)
        {
            var row = sc.Table[r];
            Put(Chip(row.Atk), r + 1, 0);
            for (int i = 0; i < n; i++) Put(MainView.MultCell(row.Mults[i], 0), r + 1, i + 1);
            Put(MainView.SumCell(row.Weak, row.Weak >= 3 ? "BadBg" : null, "Bad", 0), r + 1, n + 1);
            Put(MainView.SumCell(row.Res, row.Res > 0 ? "GoodBg" : null, "Good", 0), r + 1, n + 2);
        }
        return Surface(Col(8, SectionTitle("Defesa", "Dano recebido por tipo de golpe"), g, Muted("Fr: quantos do time são fracos. Res: quantos resistem.", 12)), 12);
    }

    Control AttackSection(List<Member> members, List<(string Type, double Best, List<string> Who)> offense)
    {
        var cells = new UniformGrid { Columns = 2 };
        foreach (var o in offense)
        {
            var key = o.Best >= 2 ? "GoodBg" : o.Best < 1 ? "BadBg" : "Panel2";
            var who = o.Best >= 2 ? o.Who[0] : "×" + TypeChart.Mult(o.Best);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,6,*") };
            var w = Muted(who, 11.5).With(t => { t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextAlignment = TextAlignment.Right; t.VerticalAlignment = VerticalAlignment.Center; });
            Grid.SetColumn(w, 2);
            g.Children.Add(Chip(o.Type)); g.Children.Add(w);
            cells.Children.Add(new Border { Padding = new Thickness(5, 5, 8, 5), Margin = new Thickness(0, 0, 5, 5), CornerRadius = new CornerRadius(9), Child = g }.Res(Border.BackgroundProperty, key));
        }
        var col = Col(12, Surface(Col(8, SectionTitle("Ataque", "Melhor dano com os golpes sugeridos"), cells,
            Muted("Verde: alguém do time acerta super efetivo. Vermelho: o melhor golpe ainda é pouco efetivo.", 12.5)), 12));
        col.Children.Add(SectionTitle("Quem completaria o time"));
        foreach (var s in Engine.Suggestions(members, 6))
        {
            bool chosen = S.Wish.Contains(s.P.Id);
            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*") };
            var info = Col(3, Text(s.P.Name, 15.5, FontWeight.ExtraBold), Chips(s.P.Types));
            Grid.SetColumn(info, 2);
            top.Children.Add(Portrait(s.P, 58)); top.Children.Add(info);
            var button = Big(chosen ? "Escolhido" : "Escolher para Montar time", () => { if (!S.Wish.Contains(s.P.Id) && S.Wish.Count < Engine.MaxCore) S.Wish.Add(s.P.Id); Save(); Render(); }, primary: false);
            button.IsEnabled = !chosen;
            col.Children.Add(Surface(Col(8, top, Muted(s.Why, 13), button), 12));
        }
        return col;
    }

    // ---------- checkbox lists shared by Gerador and Montar ----------
    Control BoxList(BoxConfig cfg, IEnumerable<string> keys, Action changed)
    {
        var col = Col(2);
        foreach (var box in Engine.Boxes.Where(b => keys.Contains(b.Key)))
        {
            col.Children.Add(ToggleRow(box.Title, box.Desc, cfg[box.Key], v =>
            {
                cfg[box.Key] = v;
                if (box.Key == "wishOnly" && v) cfg["wishPrio"] = false;
                changed();
            }));
            if (!cfg[box.Key]) continue;
            if (box.Extra == "weatherKind") col.Children.Add(SelectRow("Clima", Engine.Weathers[cfg.WeatherKind].Label, () => PickSheet("Clima", Engine.Weathers.Select(w => (w.Key, w.Value.Label)), cfg.WeatherKind, k => { cfg.WeatherKind = k; changed(); })));
            if (box.Extra == "monoType") col.Children.Add(SelectRow("Tipo", TypeChart.Pt[cfg.MonoType], () => PickSheet("Tipo", TypeChart.All.Select(t => (t, TypeChart.Pt[t])), cfg.MonoType, k => { cfg.MonoType = k; changed(); })));
            if (box.Extra == "genNum") col.Children.Add(SelectRow("Geração", $"Geração {cfg.GenNum}", () => PickSheet("Geração", Enumerable.Range(1, 9).Select(n => (n.ToString(), $"Geração {n}")), cfg.GenNum.ToString(), k => { cfg.GenNum = int.Parse(k); changed(); })));
        }
        if (cfg["fast"] && cfg["trickroom"]) col.Children.Add(AlertBox(new Alert("warn", "Rápido e Trick Room puxam em direções opostas. O resultado fica menos focado.")));
        return col;
    }

    Control ResultCard(string title, GenResult r, bool best, IEnumerable<string> why, bool reasons, params Control[] buttons)
    {
        var cores = r.Team.Where(z => z.Core).ToList();
        var sc = r.Sc ?? Engine.ScoreTeam(r.Team);
        var col = Col(10, H2(title).With(t => t.FontSize = 16),
            new WrapPanel().With(w =>
            {
                foreach (var p in new[]
                {
                    Pill($"STAB cobre {sc.Covered.Count}/18", sc.Covered.Count >= 14 ? "good" : "warn"),
                    Pill(sc.Holes.Count > 0 ? "Brechas: " + string.Join(", ", sc.Holes.Select(t => TypeChart.Pt[t])) : "Sem brechas", sc.Holes.Count > 0 ? "warn" : "good"),
                    Pill($"BST médio {Math.Round(sc.AvgBst)}", "neutral"),
                }) { p.Margin = new Thickness(0, 0, 6, 6); w.Children.Add(p); }
            }));
        foreach (var z in r.Team)
        {
            var mv = Engine.PickMoves(z.P, null, S.Tms);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*") };
            var tag = z.Core ? Pill("escolhido", "good") : z.Kept ? Pill("seu", "neutral") : null;
            var info = Col(3, Row(6, Text(z.P.Name, 15.5, FontWeight.ExtraBold, "Fg", false), tag), Chips(z.P.Types),
                Rich($"Habilidade: **{z.Ab}**", 13), Muted($"{Engine.RoleOf(z.P).Name} · BST {z.P.Bst}", 12.5));
            if (reasons && !z.Core && cores.Count > 0) info.Children.Add(Text(Engine.SupportReason(z, cores), 12.5));
            info.Children.Add(Muted(string.Join(" · ", mv.Select(m => m.Name)), 12.5));
            Grid.SetColumn(info, 2);
            g.Children.Add(Portrait(z.P, 62).With(p => p.VerticalAlignment = VerticalAlignment.Top)); g.Children.Add(info);
            col.Children.Add(new Border { Padding = new Thickness(0, 6), Child = g });
        }
        var notes = why.ToList();
        if (notes.Count > 0) col.Children.Add(Col(4, notes.Select(w => (Control)Rich("•  " + w, 13.5)).ToArray()));
        foreach (var b in buttons) col.Children.Add(b);
        var card = Surface(col, 14);
        if (best) { card.BorderThickness = new Thickness(2); card.Res(Border.BorderBrushProperty, "Accent"); }
        return card;
    }

    void UseAsTeam(List<Member> team)
    {
        int fallback = S.Team.Count > 0 ? S.Team.Max(m => m.Lv) : 50;
        S.Team = team.Select(z =>
        {
            var owned = S.Team.FirstOrDefault(m => Dex.ById[m.Id].Chain == z.P.Chain);
            return owned != null ? new TeamSlot { Id = owned.Id, Lv = owned.Lv, Ab = owned.Ab } : new TeamSlot { Id = z.P.Id, Lv = fallback, Ab = z.Ab };
        }).ToList();
        expanded = -1; teamSection = "time";
        Go("time");
    }

    // ---------- Montar time ----------
    void WishChanged() { lastBuild = null; buildShown.Clear(); Save(); Render(); }

    Control MontarPage()
    {
        bool full = S.Wish.Count >= Engine.MaxCore;
        var chosen = Col(8);
        foreach (var id in S.Wish.ToList())
        {
            var p = Dex.ById[id];
            var fin = Engine.FinalForms(p).Where(f => f.Id != p.Id).Select(f => f.Name).ToList();
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*,Auto") };
            var info = Col(2, Text(p.Name, 15, FontWeight.ExtraBold));
            if (fin.Count > 0 && S.Finals && S.Mode.Level != "lc") info.Children.Add(Muted("→ " + string.Join(" / ", fin), 12.5));
            info.VerticalAlignment = VerticalAlignment.Center;
            var remove = new Border { Width = 40, Height = 40, Background = Brushes.Transparent, Child = Text("×", 22, FontWeight.Bold, "Muted", false).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) }.Tap(() => { S.Wish.Remove(id); WishChanged(); });
            Grid.SetColumn(info, 2); Grid.SetColumn(remove, 3);
            g.Children.Add(Portrait(p, 46)); g.Children.Add(info); g.Children.Add(remove);
            chosen.Children.Add(new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(8), Child = g }.Res(Border.BackgroundProperty, "AccentSoft"));
        }
        if (S.Wish.Count == 0) chosen.Children.Add(Muted($"Nenhum Pokémon escolhido. Adicione de 1 a {Engine.MaxCore} Pokémon que você quer usar de qualquer jeito.", 14));

        var add = Big(full ? "Limite atingido" : "Adicionar Pokémon", () => PickMon("Escolha um Pokémon", p => { if (!S.Wish.Contains(p.Id) && S.Wish.Count < Engine.MaxCore) S.Wish.Add(p.Id); WishChanged(); }));
        add.IsEnabled = !full;
        var step1 = Surface(Col(10,
            SectionTitle("1. Escolha seus Pokémon", $"{S.Wish.Count}/{Engine.MaxCore} escolhidos · o resto é montado em volta deles"),
            chosen, add,
            Columns(8,
                Big("Usar meu time", () => { foreach (var m in S.Team) if (!S.Wish.Contains(m.Id) && S.Wish.Count < Engine.MaxCore) S.Wish.Add(m.Id); WishChanged(); }, primary: false),
                Big("Limpar", () => { S.Wish.Clear(); WishChanged(); }, primary: false)),
            ToggleRow("Planejar com as evoluções finais", null, S.Finals, v => { S.Finals = v; lastBuild = null; Save(); Render(); })));

        Button go = null;
        go = Big("Montar time em volta deles", () => { buildShown.Clear(); RunBusy(go, "Montando…", () => lastBuild = Engine.BuildAround(buildShown)); });
        go.IsEnabled = S.Wish.Count > 0;
        var step2 = Surface(Col(8, SectionTitle("2. Como completar o time", "Marque o estilo dos Pokémon que vão entrar junto"),
            BoxList(S.Build, Engine.BuildKeys, () => { lastBuild = null; buildShown.Clear(); Save(); Render(); }),
            go, Muted("Mostra até 3 opções diferentes.", 12.5).With(t => t.HorizontalAlignment = HorizontalAlignment.Center)));

        var col = Col(12, step1, step2);
        if (lastBuild == null) return col;
        if (lastBuild.Count == 0) col.Children.Add(AlertBox(new Alert("warn", "Nenhum Pokémon atende às caixas marcadas. Desmarque algum filtro (Monotipo, Uma geração ou Sem tipos repetidos).")));
        for (int i = 0; i < lastBuild.Count; i++)
        {
            var r = lastBuild[i];
            var card = ResultCard(i == 0 ? "Opção 1 · melhor encaixe" : $"Opção {i + 1}", r, i == 0, Engine.ExplainTeam(r.Team, S.Build), true, Big("Usar como meu time", () => UseAsTeam(r.Team)));
            if (i == 0 && wantResult) scrollTarget = card;
            col.Children.Add(card);
        }
        if (lastBuild.Count > 0)
        {
            Button again = null;
            again = Big("Ver outras opções", () => RunBusy(again, "Montando…", () => lastBuild = Engine.BuildAround(buildShown)), primary: false);
            col.Children.Add(again);
        }
        return col;
    }

    // ---------- Gerador ----------
    void Generate()
    {
        var r = Engine.GenerateBoxes(S.Gen, genShown);
        genEmpty = r == null || r.Team.Count == 0;
        genResult = genEmpty ? null : r;
    }

    Control GeradorPage()
    {
        Button go = null;
        go = Big(genResult == null ? "Gerar time" : "Gerar outro", () => RunBusy(go, "Gerando…", Generate));
        var col = Col(12, go);
        if (genEmpty) col.Children.Add(AlertBox(new Alert("warn", "Nenhum Pokémon atende a todas as caixas marcadas. Desmarque algum filtro (Monotipo, Uma geração ou Só da minha lista).")));
        else if (genResult != null)
        {
            Button wish = null;
            wish = Big("Adicionar à minha lista", () =>
            {
                foreach (var z in genResult.Team) if (S.Wish.Count < Engine.MaxCore && !S.Wish.Any(id => Dex.ById[id].Chain == z.P.Chain)) S.Wish.Add(z.P.Id);
                Save(); wish.Content = $"Na lista ({S.Wish.Count}/{Engine.MaxCore})"; wish.IsEnabled = false;
            }, primary: false);
            col.Children.Add(ResultCard($"Time gerado · {genResult.PoolSize} possíveis", genResult, true, Engine.ExplainTeam(genResult.Team, S.Gen), false,
                Big("Usar como meu time", () => UseAsTeam(genResult.Team)), wish));
        }
        col.Children.Add(Surface(Col(6, SectionTitle("Como você quer o time", "Dá para combinar várias opções"),
            BoxList(S.Gen, Engine.Boxes.Select(b => b.Key), () => { genShown.Clear(); Save(); Render(); }))));
        return col;
    }

    // ---------- Batalha ----------
    Control BatalhaPage()
    {
        var o = S.Opp;
        Mon oppMon = null; var oppTypes = new List<string>();
        if (o.Mode == "mon" && !string.IsNullOrWhiteSpace(o.Mon)) { oppMon = Dex.Find(o.Mon); if (oppMon != null) oppTypes = oppMon.Types.ToList(); }
        else if (o.Mode != "mon") oppTypes = new[] { o.T1, o.T2 }.Where(t => !string.IsNullOrEmpty(t)).ToList();
        var members = Engine.ResolveTeam();

        var pick = Col(10, SectionTitle("Contra quem você vai lutar?"),
            Segmented(new[] { ("type", "Por tipo"), ("mon", "Por Pokémon") }, o.Mode, k => { o.Mode = k; Save(); Render(); }));
        if (o.Mode == "type")
        {
            var grid = new WrapPanel();
            foreach (var t in TypeChart.All)
            {
                bool on = t == o.T1 || t == o.T2;
                grid.Children.Add(TypeButton(t, on, () =>
                {
                    if (t == o.T1) { if (!string.IsNullOrEmpty(o.T2)) { o.T1 = o.T2; o.T2 = ""; } }
                    else if (t == o.T2) o.T2 = "";
                    else o.T2 = t;
                    Save(); Render();
                }).With(c => c.Margin = new Thickness(0, 0, 6, 6)));
            }
            pick.Children.Add(Muted("Toque em até 2 tipos (ginásio, líder).", 13));
            pick.Children.Add(grid);
        }
        else if (oppMon != null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*") };
            var info = Col(3, Text(oppMon.Name, 16, FontWeight.ExtraBold), Chips(oppTypes), Muted($"VEL {oppMon.Stats[5]} · BST {oppMon.Bst}", 12.5));
            Grid.SetColumn(info, 2);
            g.Children.Add(Portrait(oppMon, 62)); g.Children.Add(info);
            pick.Children.Add(g);
        }
        if (o.Mode == "mon") pick.Children.Add(Big(oppMon == null ? "Escolher Pokémon adversário" : "Trocar adversário", () => PickMon("Pokémon adversário", p => { o.Mon = p.Name; Save(); Render(); }), primary: oppMon == null));
        if (oppTypes.Count > 0)
            pick.Children.Add(Col(5, Label("Tipos que acertam em cheio"), new WrapPanel().With(w =>
            {
                foreach (var a in TypeChart.All.Where(a => oppTypes.Aggregate(1.0, (m, t) => m * TypeChart.Eff(a, t)) >= 2)) w.Children.Add(Chip(a).With(c => c.Margin = new Thickness(0, 0, 4, 4)));
            })));

        var col = Col(12, Surface(pick));
        col.Children.Add(SectionTitle("Quem mandar", "Ordenado por dano causado ÷ dano recebido"));
        if (oppTypes.Count > 0 && members.Count > 0)
        {
            var ranked = Engine.RankVs(members, oppTypes, oppMon);
            var md = S.Mode;
            int active = Engine.ActiveCount;
            var bring = ranked.Take(Math.Min(md.Pick, ranked.Count)).ToList();
            if (md.Pick < ranked.Count || active > 1)
            {
                var leads = bring.Take(active).ToList();
                var why = active > 1 ? leads.Select(r => { var f = Engine.FeatOf(r.X.P); return f.Fake ? "Fake Out" : f.Speed ? "controle de velocidade" : r.X.Ab == "Intimidate" ? "Intimidate" : null; }).Where(w => w != null).Distinct().ToList() : new();
                col.Children.Add(AlertBox(new Alert("good", (md.Pick < ranked.Count ? $"Leve **{md.Pick}**: {Engine.Names(bring.Select(r => r.X))}. " : "") + $"Comece com {string.Join(" + ", leads.Select(r => $"**{r.X.P.Name}**"))}" + (why.Count > 0 ? $" ({string.Join(", ", why)})" : "") + ".")));
            }
            int pos = 1;
            foreach (var r in ranked)
            {
                var verdict = r.Best != null && r.Mult >= 2 && r.Incoming <= 1 ? ("good", "Ótima escolha") : r.Incoming >= 2 && (r.Best == null || r.Mult < 2) ? ("bad", "Evite") : r.Incoming >= 2 ? ("warn", "Arriscado") : ("neutral", "Ok");
                var detail = (r.Best != null ? $"Use **{r.Best.Name}** ({TypeChart.Pt[r.Best.Type]}, ×{TypeChart.Mult(r.Mult)})" : "Sem golpe de dano") + $" · recebe até ×{TypeChart.Mult(r.Incoming)} de STAB" + (r.Speed != null ? $" · {r.Speed}" : "");
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*") };
                var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                var pill = Pill(verdict.Item2, verdict.Item1);
                Grid.SetColumn(pill, 1);
                top.Children.Add(Rich($"**{pos++}. {r.X.P.Name}**  Nv {Engine.BattleLv(r.X.Lv)}", 15)); top.Children.Add(pill);
                var info = Col(4, top, Rich(detail, 13));
                Grid.SetColumn(info, 2);
                g.Children.Add(Portrait(r.X.P, 54)); g.Children.Add(info);
                col.Children.Add(Surface(g, 12));
            }
        }
        else col.Children.Add(Surface(Muted("Escolha um adversário e tenha pelo menos um Pokémon no time.", 14)));

        var md2 = S.Mode;
        var tips = Engine.FormatTips[md2.Format].ToList();
        if (md2.Pick < md2.Size) tips.Add($"Você monta {md2.Size} e escolhe {md2.Pick} depois de ver o time adversário. Leve quem tem vantagem de tipo contra eles.");
        if (md2.Level == "50" || md2.Level == "100") tips.Add($"Todos lutam no nível {Engine.BattleLv(1)}, então a diferença de nível some e o que conta são atributos, tipos e golpes.");
        if (md2.Level == "lc") tips.Add("Little Cup: só Pokémon no 1º estágio, no nível 5. Atributos de defesa e velocidade pesam muito.");
        if (md2.SleepClause) tips.Add("Cláusula de sono: golpes que fazem dormir (Spore, Sleep Powder, Hypnosis, Yawn) ficam fora das sugestões.");
        col.Children.Add(Surface(Col(8, SectionTitle("Como jogar: " + Engine.FormatLabel[md2.Format], Engine.ModeSummary()), Col(5, tips.Select(t => (Control)Rich("•  " + t, 13.5)).ToArray()))));
        return col;
    }

    // ---------- Sobre ----------
    Control SobrePage()
    {
        updateBtn = Big(flow.ButtonLabel, flow.Check);
        updateBtn.IsEnabled = !flow.Busy;

        var hero = new Border
        {
            CornerRadius = new CornerRadius(18), Padding = new Thickness(18),
            Child = Col(8, Brand.Logo(96).With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
                Brand.Wordmark(28, "White").With(w => w.HorizontalAlignment = HorizontalAlignment.Center),
                new TextBlock { Text = "Seu time, bem empilhado.", FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = $"Versão {Updater.Version}", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center }),
        }.Res(Border.BackgroundProperty, "Ink");

        return Col(12, hero,
            Surface(Col(10, SectionTitle("O que é o Palet Mon"),
                Text("Um estrategista de time para o Cobbleverse, o modpack de Pokémon para Minecraft. Ele mostra o que seu time aguenta, o que falta e quem levar para cada luta.", 14),
                Rich("**Meu time:** fraquezas, cobertura de ataque, golpes sugeridos e quando cada Pokémon evolui.", 14),
                Rich("**Montar:** você escolhe os Pokémon que quer usar e o app completa o resto em volta deles.", 14),
                Rich("**Gerador:** cria um time do zero, como Aleatório, Sinergia ou Competitivo.", 14),
                Rich("**Batalha:** diz quem mandar contra um tipo ou um Pokémon adversário.", 14),
                Rich("**Modo de combate:** toque no botão do topo para ajustar tudo para Singles, Duplas, Triplas e regras como Nv 50 ou Little Cup.", 14))),
            Surface(Col(10, SectionTitle("Atualizações"),
                Text("Sempre que abre, o app procura uma versão nova e pergunta se você quer atualizar. Para isso, ele precisa de internet. Seu time e suas opções continuam salvos.", 14),
                updateBtn,
                Muted($"Você está na versão {Updater.Version}.", 13).With(t => t.HorizontalAlignment = HorizontalAlignment.Center))),
            Surface(Col(10, SectionTitle("Créditos"),
                Text("Dados e imagens dos Pokémon vêm da PokéAPI. As imagens são baixadas pela internet e ficam guardadas no aparelho.", 14),
                Text("Palet Mon é um projeto de fã, sem ligação com Nintendo, The Pokémon Company ou Cobblemon.", 14),
                Text("Golpes marcados \"TM/tutor/ovo\" precisam de TM, tutor ou reprodução. O Cobblemon segue os dados de perto, mas alguns métodos de evolução mudam no mod.", 13, null, "Muted"),
                Big("Abrir no GitHub", () => OpenLink(this, Updater.RepoUrl), primary: false))));
    }
}

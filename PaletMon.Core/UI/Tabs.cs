using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PaletMon.Data;
using PaletMon.Logic;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

public sealed partial class MainView
{
    List<GenResult> lastBuild;
    GenResult genResult;
    bool genEmpty;
    readonly HashSet<string> buildShown = new();
    readonly HashSet<string> genShown = new();

    // ---------- Meu time ----------
    Control TimeTab()
    {
        var members = Engine.ResolveTeam();
        foreach (var x in members) x.Moves = Engine.PickMoves(x.P, Engine.BattleLv(x.Lv), S.Tms);
        var sc = Engine.ScoreTeam(members);
        var alerts = Engine.TeamAlerts(members, sc);
        var offense = Engine.Offense(members);
        int coveredN = offense.Count(o => o.Best >= 2);

        var summary = Card(Col(12,
            PanelHead(H2("Resumo do time"), Row(6,
                Pill($"Cobertura {coveredN}/18 tipos", coveredN >= 14 ? "good" : coveredN >= 10 ? "warn" : "bad"),
                Pill($"{sc.Holes.Count} brecha{(sc.Holes.Count == 1 ? "" : "s")} defensiva{(sc.Holes.Count == 1 ? "" : "s")}", sc.Holes.Count == 0 ? "good" : sc.Holes.Count <= 2 ? "warn" : "bad"),
                Pill($"BST médio {Math.Round(sc.AvgBst)}", "neutral"))),
            Col(8, alerts.Count > 0 ? alerts.Select(a => (Control)AlertBox(a)).ToArray() : new Control[] { AlertBox(new Alert("good", "Nenhum problema grave encontrado.")) }),
            Check("Considerar golpes de TM, tutor e ovo nas sugestões", S.Tms, v => { S.Tms = v; Save(); Render(); })));

        var cards = members.Select((x, i) => MonCard(x, i)).ToList();
        if (members.Count < 6) cards.Add(AddCard(members));

        var defense = DefenseCard(members, sc);
        var attack = AttackCard(members, offense);
        var two = new Grid { ColumnDefinitions = new ColumnDefinitions("1.25*,16,*") };
        Grid.SetColumn(attack, 2);
        two.Children.Add(defense); two.Children.Add(attack);

        return Col(16, summary, Wrap(14, cards), two);
    }

    Control MonCard(Member x, int i)
    {
        var lv = Stepper(x.Lv, v => { S.Team[i].Lv = v; Save(); Render(); });
        var remove = Btn("Remover", () => { S.Team.RemoveAt(i); Save(); Render(); }, primary: false, small: true);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("46,10,*,10,Auto") };
        var name = Col(4, Text(x.P.Name, 17, FontWeight.Normal).With(t => t.FontFamily = Display), Chips(x.P.Types));
        Grid.SetColumn(name, 2);
        var right = Col(6, Row(4, Muted("Nv", 12).With(t => t.VerticalAlignment = VerticalAlignment.Center), lv), remove);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(right, 4);
        head.Children.Add(Badge(x.P)); head.Children.Add(name); head.Children.Add(right);

        var role = Engine.RoleOf(x.P);
        var abilities = x.P.Abilities.Select(a => (a.TrimStart('*'), a.TrimStart('*') + (a.StartsWith("*") ? " (oculta)" : "")));
        var ab = Combo(abilities, x.Ab, k => { S.Team[i].Ab = k; Save(); Render(); }, 170);
        var roleRow = PanelHead(Rich($"Papel: **{role.Name}**", 13, "Muted"), ab);

        var moveLabel = $"Golpes sugeridos no nível {Engine.BattleLv(x.Lv)}" + (Engine.IsMulti ? $" · {Engine.FormatLabel[S.Mode.Format].ToLowerInvariant()}" : "");
        var movesGrid = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var m in x.Moves) movesGrid.Children.Add(MoveCard(m));
        Control moves = x.Moves.Count > 0 ? movesGrid : Muted("Sem golpes de dano disponíveis ainda.");

        var card = Card(Col(12, head, StatBars(x.P), roleRow, Muted(role.Tip, 12.5), EvoBlock(x.P, x.Lv), Col(6, Label(moveLabel), moves)), 14);
        card.Width = 380;
        return card;
    }

    // compact level picker: − value +, with the value editable by typing
    static Control Stepper(int value, Action<int> changed)
    {
        int Clamp(int v) => Math.Max(1, Math.Min(100, v));
        var box = new TextBox { Text = value.ToString(), Width = 46, MinHeight = 28, Padding = new Thickness(4, 4), TextAlignment = TextAlignment.Center, FontFamily = Mono, VerticalContentAlignment = VerticalAlignment.Center };
        void Commit() { if (int.TryParse(box.Text, out var v) && Clamp(v) != value) changed(Clamp(v)); else box.Text = value.ToString(); }
        box.LostFocus += (_, _) => Commit();
        box.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Commit(); };
        Button Step(string t, int d)
        {
            var b = new Button { Content = t, Width = 28, MinHeight = 28, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold };
            b.Click += (_, _) => { var v = Clamp(value + d); if (v != value) { value = v; box.Text = v.ToString(); changed(v); } };
            return b;
        }
        return Row(4, Step("−", -1), box, Step("+", 1));
    }

    internal static Control EvoBlock(Mon p, int lv)
    {
        var kids = Engine.NextEvos(p);
        Border box(Control c, bool ready) => new Border { Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(8), Child = c }.Res(Border.BackgroundProperty, ready ? "GoodBg" : "Panel2");
        if (kids.Count == 0) return box(Rich($"**Forma final** · BST {p.Bst}", 13), false);
        bool anyReady = false;
        var lines = Col(6);
        foreach (var k in kids)
        {
            bool ready = k.Evo != null && k.Evo.Level > 0 && lv >= k.Evo.Level;
            anyReady |= ready;
            var finals = Engine.FinalForms(k).Where(f => f.Id != k.Id).Select(f => f.Name).ToList();
            var line = new WrapPanel();
            if (ready) line.Children.Add(Pill("Pronto para evoluir", "good").With(b => b.Margin = new Thickness(0, 0, 6, 0)));
            line.Children.Add(Rich($"→ **{k.Name}**", 13).With(t => t.Margin = new Thickness(0, 0, 6, 0)));
            line.Children.Add(Chips(k.Types).With(c => c.Margin = new Thickness(0, 0, 6, 0)));
            line.Children.Add(Muted("· " + Engine.EvoText(k.Evo) + (finals.Count > 0 ? "  → " + string.Join(" / ", finals) : "")));
            lines.Children.Add(line);
        }
        return box(lines, anyReady);
    }

    Control AddCard(List<Member> members)
    {
        var search = Search("Nome do Pokémon");
        int newLv = members.Count > 0 ? members.Max(m => m.Lv) : 5;
        var lv = Stepper(newLv, v => newLv = v);
        var msg = Muted("");
        void Add()
        {
            var p = Dex.Find(search.Text);
            if (p == null) { msg.Text = "Não encontrei esse Pokémon. Escolha um nome da lista."; return; }
            S.Team.Add(new TeamSlot { Id = p.Id, Lv = newLv }); Save(); Render();
        }
        search.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Add(); };
        var card = new Border
        {
            Width = 380, Padding = new Thickness(16), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1.5),
            Child = Col(8, Label($"Adicionar ao time ({members.Count}/6)"), search, Row(8, lv, Btn("Adicionar", Add, small: true)), msg),
        }.Res(Border.BorderBrushProperty, "Line");
        return card;
    }

    Control DefenseCard(List<Member> members, TeamScore sc)
    {
        if (members.Count == 0) return Card(Col(10, H2("Defesa"), Muted("Adicione Pokémon para ver a tabela.")));
        int n = members.Count;
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto," + string.Join(",", Enumerable.Repeat("Auto", n + 2))) };
        g.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", 19)));
        void Put(Control c, int r, int col) { Grid.SetRow(c, r); Grid.SetColumn(c, col); c.Margin = new Thickness(2); g.Children.Add(c); }
        for (int i = 0; i < n; i++) Put(Muted(members[i].P.Name.Length > 7 ? members[i].P.Name[..7] : members[i].P.Name, 11.5).With(t => t.HorizontalAlignment = HorizontalAlignment.Center), 0, i + 1);
        Put(Muted("Fraco", 11.5), 0, n + 1); Put(Muted("Resiste", 11.5), 0, n + 2);
        for (int r = 0; r < 18; r++)
        {
            var row = sc.Table[r];
            Put(Chip(row.Atk), r + 1, 0);
            for (int i = 0; i < n; i++) Put(MultCell(row.Mults[i]), r + 1, i + 1);
            Put(SumCell(row.Weak, row.Weak >= 3 ? "BadBg" : null, "Bad"), r + 1, n + 1);
            Put(SumCell(row.Res, row.Res > 0 ? "GoodBg" : null, "Good"), r + 1, n + 2);
        }
        return Card(Col(10, PanelHead(H2("Defesa"), Muted("Dano recebido por tipo de golpe")), g));
    }

    internal static Control MultCell(double v, double minWidth = 40)
    {
        var (bg, fg) = v == 0 ? ("ImmBg", "Fg") : v >= 4 ? ("Bad", "Panel") : v >= 2 ? ("BadBg", "Bad") : v <= 0.25 ? ("Good", "Panel") : v < 1 ? ("GoodBg", "Good") : (null, "Muted");
        var b = new Border { MinWidth = minWidth, CornerRadius = new CornerRadius(5), Padding = new Thickness(minWidth < 40 ? 0 : 4, 2), Child = Text(TypeChart.Mult(v), 12.5, v == 1 ? FontWeight.Normal : FontWeight.Bold, fg, false).With(t => { t.FontFamily = Mono; t.HorizontalAlignment = HorizontalAlignment.Center; }) };
        if (bg != null) b.Res(Border.BackgroundProperty, bg);
        return b;
    }
    internal static Control SumCell(int v, string bg, string fg, double minWidth = 40)
    {
        var b = new Border { MinWidth = minWidth, CornerRadius = new CornerRadius(5), Padding = new Thickness(minWidth < 40 ? 0 : 4, 2), Child = Text(v.ToString(), 12.5, FontWeight.Bold, bg != null ? fg : "Muted", false).With(t => t.HorizontalAlignment = HorizontalAlignment.Center) };
        if (bg != null) b.Res(Border.BackgroundProperty, bg);
        return b;
    }

    Control AttackCard(List<Member> members, List<(string Type, double Best, List<string> Who)> offense)
    {
        var cells = offense.Select(o =>
        {
            var key = o.Best >= 2 ? "GoodBg" : o.Best < 1 ? "BadBg" : "Panel2";
            var who = o.Best >= 2 ? (o.Who[0].Length > 9 ? o.Who[0][..9] : o.Who[0]) : "×" + TypeChart.Mult(o.Best);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Width = 150 };
            var w = Muted(who, 11).With(t => { t.FontFamily = Mono; t.HorizontalAlignment = HorizontalAlignment.Right; t.VerticalAlignment = VerticalAlignment.Center; });
            Grid.SetColumn(w, 1);
            g.Children.Add(Chip(o.Type)); g.Children.Add(w);
            var b = new Border { Padding = new Thickness(4, 4, 8, 4), CornerRadius = new CornerRadius(8), Child = g }.Res(Border.BackgroundProperty, key);
            ToolTip.SetTip(b, string.Join(", ", o.Who));
            return (Control)b;
        });
        var sugg = Engine.Suggestions(members, 6).Select(s =>
        {
            bool chosen = S.Wish.Contains(s.P.Id);
            var btn = Btn(chosen ? "Escolhido" : "Escolher para Montar time", () => { if (!S.Wish.Contains(s.P.Id) && S.Wish.Count < Engine.MaxCore) S.Wish.Add(s.P.Id); Save(); Render(); }, primary: false, small: true);
            btn.IsEnabled = !chosen;
            var card = Card(Col(6, PanelHead(Text(s.P.Name, 14, FontWeight.ExtraBold), Chips(s.P.Types)), Muted(s.Why, 12.5), btn), 10, "Bg");
            card.Width = 250;
            return (Control)card;
        });
        return Card(Col(12,
            PanelHead(H2("Ataque"), Muted("Melhor dano com os golpes sugeridos")),
            Wrap(6, cells),
            Muted("Verde: alguém do time acerta super efetivo. Vermelho: o melhor golpe do time ainda é pouco efetivo.", 12.5),
            H3("Quem completaria o time"),
            Wrap(10, sugg)));
    }

    // ---------- Montar time ----------
    Control MontarTab()
    {
        var chosen = S.Wish.Select(id => Dex.ById[id]).ToList();
        bool full = S.Wish.Count >= Engine.MaxCore;
        var search = Search(full ? "Limite atingido" : "Ex.: Garchomp, Gardevoir, Lucario");
        search.IsEnabled = !full;
        var msg = Muted("");
        void Add()
        {
            var p = Dex.Find(search.Text);
            if (p == null) { msg.Text = "Não encontrei esse Pokémon. Escolha um nome da lista."; return; }
            if (!S.Wish.Contains(p.Id) && S.Wish.Count < Engine.MaxCore) S.Wish.Add(p.Id);
            lastBuild = null; buildShown.Clear(); Save(); Render();
        }
        search.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Add(); };

        var items = chosen.Select(p =>
        {
            var fin = Engine.FinalForms(p).Where(f => f.Id != p.Id).Select(f => f.Name).ToList();
            var x = new Button { Content = "×", Padding = new Thickness(6, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            x.Click += (_, _) => { S.Wish.Remove(p.Id); lastBuild = null; buildShown.Clear(); Save(); Render(); };
            var label = Row(6, Text(p.Name, 13.5, FontWeight.Bold, "Fg", false), fin.Count > 0 && S.Finals && S.Mode.Level != "lc" ? Muted("→ " + string.Join(" / ", fin), 12) : null, x);
            label.VerticalAlignment = VerticalAlignment.Center;
            return (Control)new Border { CornerRadius = new CornerRadius(99), Padding = new Thickness(12, 3, 4, 3), Child = label }.Res(Border.BackgroundProperty, "AccentSoft");
        }).ToList();

        var step1 = Card(Col(12,
            PanelHead(H2("1. Escolha seus Pokémon"), Muted($"{S.Wish.Count}/{Engine.MaxCore} escolhidos · o resto do time é montado em volta deles")),
            Row(8, search, Btn("Adicionar", Add, small: true).With(b => b.IsEnabled = !full),
                Btn("Usar meu time atual", () => { foreach (var m in S.Team) if (!S.Wish.Contains(m.Id) && S.Wish.Count < Engine.MaxCore) S.Wish.Add(m.Id); lastBuild = null; buildShown.Clear(); Save(); Render(); }, primary: false, small: true),
                Btn("Limpar", () => { S.Wish.Clear(); lastBuild = null; buildShown.Clear(); Save(); Render(); }, primary: false, small: true)),
            msg,
            items.Count > 0 ? Wrap(8, items) : Muted($"Nenhum Pokémon escolhido. Adicione 1 a {Engine.MaxCore} Pokémon que você quer usar de qualquer jeito."),
            Check("Planejar com as evoluções finais", S.Finals, v => { S.Finals = v; lastBuild = null; Save(); Render(); })));

        Button go = null;
        go = Btn("Montar time em volta deles", () => { buildShown.Clear(); RunBusy(go, "Montando…", () => lastBuild = Engine.BuildAround(buildShown)); });
        go.IsEnabled = S.Wish.Count > 0;
        var step2 = Card(Col(12,
            PanelHead(H2("2. Como completar o time"), Muted("Marque o estilo dos Pokémon que vão entrar junto")),
            BoxGrid(S.Build, Engine.BuildKeys, () => { lastBuild = null; buildShown.Clear(); Save(); Render(); }),
            Row(10, go, Muted("Mostra até 3 opções diferentes.").With(t => t.VerticalAlignment = VerticalAlignment.Center))));

        var col = Col(16, step1, step2);
        if (lastBuild != null)
        {
            if (lastBuild.Count == 0) col.Children.Add(Card(Text("Nenhum Pokémon atende às caixas marcadas. Desmarque algum filtro (Monotipo, Uma geração ou Sem tipos repetidos).")));
            for (int i = 0; i < lastBuild.Count; i++)
            {
                var r = lastBuild[i];
                col.Children.Add(TeamResult(i == 0 ? "Opção 1 · melhor encaixe" : $"Opção {i + 1}", r, i == 0, Engine.ExplainTeam(r.Team, S.Build),
                    Row(10, Btn("Usar como meu time", () => UseAsTeam(r.Team), small: true), Muted("Mantém o nível dos que já estão no seu time.").With(t => t.VerticalAlignment = VerticalAlignment.Center)), true));
            }
            if (lastBuild.Count > 0)
            {
                Button again = null;
                again = Btn("Ver outras opções", () => RunBusy(again, "Montando…", () => lastBuild = Engine.BuildAround(buildShown)), primary: false);
                col.Children.Add(again);
            }
        }
        return col;
    }

    // ---------- Gerador ----------
    Control GeradorTab()
    {
        Button go = null;
        go = Btn("Gerar time", () => RunBusy(go, "Gerando…", Generate));
        var col = Col(16, Card(Col(12,
            PanelHead(H2("Gerador de time"), Muted("Marque as caixas com o que você quer e gere. Dá para combinar várias.")),
            BoxGrid(S.Gen, Engine.Boxes.Select(b => b.Key), () => { genShown.Clear(); Save(); Render(); }),
            Row(10, go, Muted(S.Tms ? "Os golpes mostrados consideram todo o learnset, incluindo TMs, tutores e ovo." : "Os golpes mostrados consideram todo o learnset.").With(t => t.VerticalAlignment = VerticalAlignment.Center)))));
        if (genEmpty) col.Children.Add(Card(Text("Nenhum Pokémon atende a todas as caixas marcadas. Desmarque algum filtro (Monotipo, Uma geração ou Só da minha lista).")));
        else if (genResult != null)
        {
            Button again = null, wish = null;
            again = Btn("Gerar outro", () => RunBusy(again, "Gerando…", Generate), small: true);
            wish = Btn("Adicionar à minha lista", () =>
            {
                foreach (var z in genResult.Team) if (S.Wish.Count < Engine.MaxCore && !S.Wish.Any(id => Dex.ById[id].Chain == z.P.Chain)) S.Wish.Add(z.P.Id);
                Save(); wish.Content = $"Na lista ({S.Wish.Count}/{Engine.MaxCore})"; wish.IsEnabled = false;
            }, primary: false, small: true);
            var buttons = Row(8, again, Btn("Usar como meu time", () => UseAsTeam(genResult.Team), primary: false, small: true), wish);
            col.Children.Add(TeamResult($"Time gerado · {genResult.PoolSize} Pokémon possíveis com essas caixas", genResult, true, Engine.ExplainTeam(genResult.Team, S.Gen), buttons, false));
        }
        return col;
    }

    void Generate()
    {
        var r = Engine.GenerateBoxes(S.Gen, genShown);
        genEmpty = r == null || r.Team.Count == 0;
        genResult = genEmpty ? null : r;
    }

    // ---------- Batalha ----------
    Control BatalhaTab()
    {
        var o = S.Opp;
        Mon oppMon = null; var oppTypes = new List<string>();
        if (o.Mode == "mon" && !string.IsNullOrWhiteSpace(o.Mon)) { oppMon = Dex.Find(o.Mon); if (oppMon != null) oppTypes = oppMon.Types.ToList(); }
        else oppTypes = new[] { o.T1, o.T2 }.Where(t => !string.IsNullOrEmpty(t)).ToList();
        var members = Engine.ResolveTeam();

        var byType = new RadioButton { Content = "Por tipo (ginásio, líder)", GroupName = "opp", IsChecked = o.Mode == "type" };
        var byMon = new RadioButton { Content = "Por Pokémon", GroupName = "opp", IsChecked = o.Mode == "mon" };
        byType.IsCheckedChanged += (_, _) => { if (byType.IsChecked == true && o.Mode != "type") { o.Mode = "type"; Save(); Render(); } };
        byMon.IsCheckedChanged += (_, _) => { if (byMon.IsChecked == true && o.Mode != "mon") { o.Mode = "mon"; Save(); Render(); } };
        var types = TypeChart.All.Select(t => (t, TypeChart.Pt[t]));
        Control picker;
        if (o.Mode == "type")
            picker = Row(12, Field("Tipo 1", Combo(types, o.T1, k => { o.T1 = k; Save(); Render(); }, 160)),
                Field("Tipo 2", Combo(types.Prepend(("", "—")), o.T2, k => { o.T2 = k; Save(); Render(); }, 160)));
        else
        {
            var s = Search("Ex.: Gyarados"); s.Text = o.Mon;
            void Set() { o.Mon = s.Text; Save(); Render(); }
            s.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Set(); };
            s.DropDownClosed += (_, _) => { if (s.Text != o.Mon && Dex.Find(s.Text) != null) Set(); };
            picker = Field("Pokémon adversário", s);
        }
        var left = Col(12, H2("Contra quem você vai lutar?"), Row(16, byType, byMon), picker);
        if (oppTypes.Count > 0)
        {
            left.Children.Add(Row(8, Label("Adversário").With(t => t.VerticalAlignment = VerticalAlignment.Center), Chips(oppTypes), oppMon != null ? Muted($"VEL {oppMon.Stats[5]} · BST {oppMon.Bst}", 12) : null));
            left.Children.Add(Col(4, Label("Tipos que acertam em cheio"), Chips(TypeChart.All.Where(a => oppTypes.Aggregate(1.0, (m, t) => m * TypeChart.Eff(a, t)) >= 2))));
        }

        var right = Col(10, PanelHead(H2("Quem mandar"), Muted("Ordenado por dano causado ÷ dano recebido")));
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
                var text = (md.Pick < ranked.Count ? $"Leve **{md.Pick}**: {Engine.Names(bring.Select(r => r.X))}. " : "") + $"Comece com {string.Join(" + ", leads.Select(r => $"**{r.X.P.Name}**"))}" + (why.Count > 0 ? $" ({string.Join(", ", why)})" : "") + ".";
                right.Children.Add(AlertBox(new Alert("good", text)));
            }
            int pos = 1;
            foreach (var r in ranked)
            {
                var verdict = r.Best != null && r.Mult >= 2 && r.Incoming <= 1 ? ("good", "Ótima escolha") : r.Incoming >= 2 && (r.Best == null || r.Mult < 2) ? ("bad", "Evite") : r.Incoming >= 2 ? ("warn", "Arriscado") : ("neutral", "Ok");
                var detail = (r.Best != null ? $"Use **{r.Best.Name}** ({TypeChart.Pt[r.Best.Type]}, ×{TypeChart.Mult(r.Mult)})" : "Sem golpe de dano") + $" · recebe até ×{TypeChart.Mult(r.Incoming)} de STAB" + (r.Speed != null ? $" · {r.Speed}" : "");
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("32,10,*,10,Auto") };
                var num = Text(pos++.ToString(), 18, FontWeight.Normal, "Muted", false).With(t => { t.FontFamily = Display; t.VerticalAlignment = VerticalAlignment.Center; });
                var mid = Col(2, Rich($"**{r.X.P.Name}**  Nv {Engine.BattleLv(r.X.Lv)}", 14), Rich(detail, 12.5));
                Grid.SetColumn(mid, 2);
                var pill = Pill(verdict.Item2, verdict.Item1);
                Grid.SetColumn(pill, 4);
                g.Children.Add(num); g.Children.Add(mid); g.Children.Add(pill);
                right.Children.Add(Card(g, 10, "Bg"));
            }
        }
        else right.Children.Add(Muted("Escolha um adversário e tenha pelo menos um Pokémon no time."));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        var rightCard = Card(right);
        Grid.SetColumn(rightCard, 2);
        grid.Children.Add(Card(left)); grid.Children.Add(rightCard);

        var md2 = S.Mode;
        var tips = Engine.FormatTips[md2.Format].ToList();
        if (md2.Pick < md2.Size) tips.Add($"Você monta {md2.Size} e escolhe {md2.Pick} depois de ver o time adversário. Leve quem tem vantagem de tipo contra eles.");
        if (md2.Level == "50" || md2.Level == "100") tips.Add($"Todos lutam no nível {Engine.BattleLv(1)}, então a diferença de nível some e o que conta são atributos, tipos e golpes.");
        if (md2.Level == "lc") tips.Add("Little Cup: só Pokémon no 1º estágio, no nível 5. Atributos de defesa e velocidade pesam muito.");
        if (md2.SleepClause) tips.Add("Cláusula de sono: golpes que fazem dormir (Spore, Sleep Powder, Hypnosis, Yawn) ficam fora das sugestões.");
        var tipCard = Card(Col(10, PanelHead(H2("Como jogar: " + Engine.FormatLabel[md2.Format]), Muted(Engine.ModeSummary())), Col(4, tips.Select(t => (Control)Rich("•  " + t, 13.5)).ToArray())));
        return Col(16, grid, tipCard);
    }
}

using System.Globalization;
using System.Text.Json;
using PaletMon.Data;
using PaletMon.Logic;

// Dumps deterministic engine results, one fact per line. ParityTest.kt builds the same lines from
// the Kotlin engine, so any difference between the desktop and Android rules fails the Android tests.
static class Golden
{
    static readonly List<string> Lines = new();

    static string N(double v) => ((long)Math.Round(v * 10000)).ToString(CultureInfo.InvariantCulture);
    static string B(bool v) => v ? "1" : "0";
    static string J(IEnumerable<string> xs) => string.Join(",", xs);
    static string Ids(IEnumerable<Mon> xs) => J(xs.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)));
    static void Add(params object[] parts) => Lines.Add(string.Join("|", parts));

    public static void Write(string path)
    {
        Dex.Load();
        Engine.S = new AppState();
        foreach (var p in Dex.All) Mon(p);
        foreach (var preset in new[] { "livre", "vgc", "comp" })
        {
            Engine.S = new AppState();
            Engine.ApplyPreset(preset);
            foreach (var p in Dex.All) Moves(preset, p);
        }

        var wish = new List<int> { 445, 282, 6, 130, 25 };
        foreach (var preset in Engine.Presets.Keys)
        {
            Engine.S = new AppState { Wish = wish };
            Engine.ApplyPreset(preset);
            Add("mode", preset, Engine.ModeSummary(), Engine.MaxCore, Engine.ActiveCount, Engine.BattleLv(37));
            foreach (var (name, cfg) in Configs()) Pool(preset, name, cfg);
            int t = 0;
            foreach (var team in Teams()) Team($"{preset}/{t++}", team);
        }

        File.WriteAllLines(path, Lines);
        File.WriteAllText(path + ".state.json", JsonSerializer.Serialize(SavedState(), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{Lines.Count} linhas");
    }

    static void Mon(Mon p)
    {
        var role = Engine.RoleOf(p);
        var f = Engine.FeatOf(p);
        Add("mon", p.Id, p.Name, p.Bst, role.Name, role.Tip, Engine.StyleOf(p), Engine.DefaultAbility(p), J(Engine.AbilityNames(p)),
            Ids(Engine.NextEvos(p)), Ids(Engine.FinalForms(p)), Engine.EvoText(p.Evo), B(Engine.IsFinal(p)), B(Engine.IsBasic(p)),
            f.Spread, B(f.Fake), B(f.Redirect), B(f.Speed), B(f.Eq));
        Ev("ev", p);
    }

    static void Ev(string tag, Mon p)
    {
        var b = Engine.BuildOf(p);
        Add("ev", tag, p.Id, b.Target.Id, Engine.EvText(b), Engine.NatureText(b));
    }

    static void Moves(string preset, Mon p)
    {
        foreach (var lv in new int?[] { null, 5, 30, 60, 100 })
            foreach (var tms in new[] { true, false })
                Add("moves", preset, p.Id, lv?.ToString(CultureInfo.InvariantCulture) ?? "-", B(tms),
                    J(Engine.PickMoves(p, lv, tms).Select(m => $"{m.Name}:{m.Src}:{N(Engine.MoveValue(p, m))}")));
    }

    static void Pool(string preset, string name, BoxConfig cfg)
    {
        var pool = Engine.GenPool(cfg);
        long sum = 0;
        for (int i = 0; i < pool.Count; i++) sum = (sum + (long)pool[i].Id * (i + 1)) % 1000000007;
        Add("pool", preset, name, pool.Count, sum, J(pool.Where((_, i) => i % 7 == 0).Select(p => Engine.BestAbility(p, cfg))));
    }

    static void Team(string tag, List<TeamSlot> team)
    {
        var s = Engine.S;
        s.Team = team;
        var members = Engine.ResolveTeam();
        foreach (var x in members) x.Moves = Engine.PickMoves(x.P, Engine.BattleLv(x.Lv), s.Tms);
        var sc = Engine.ScoreTeam(members);

        foreach (var x in members) Ev(tag, x.P);
        Add("score", tag, N(sc.Score), N(sc.DefPen), N(sc.DupPen), N(sc.AvgBst), J(sc.Covered), J(sc.Holes), J(sc.TypeCount.Select(kv => $"{kv.Key}={kv.Value}")));
        foreach (var r in sc.Table) Add("def", tag, r.Atk, J(r.Mults.Select(N)), r.Weak, r.Res);
        foreach (var a in Engine.TeamAlerts(members, sc)) Add("alert", tag, a.Kind, a.Text);
        foreach (var o in Engine.Offense(members)) Add("off", tag, o.Type, N(o.Best), J(o.Who));
        foreach (var x in Engine.Suggestions(members, 6)) Add("sug", tag, x.P.Id, x.Why);
        if (members.Count == 0) return;

        Add("subset", tag, Engine.Names(Engine.BestSubset(members, Math.Min(s.Mode.Pick, members.Count))));
        foreach (var (name, types, mon) in Opponents())
            foreach (var r in Engine.RankVs(members, types, mon))
                Add("rank", tag, name, r.X.P.Id, r.Best?.Name ?? "-", N(r.Mult), N(r.Incoming), r.Speed ?? "-", N(r.Score));
        foreach (var (name, cfg) in Configs())
        {
            Add("gs", tag, name, N(Engine.GenScore(members, cfg)));
            foreach (var w in Engine.ExplainTeam(members, cfg)) Add("why", tag, name, w);
        }
        var cores = members.Take(Math.Max(1, members.Count / 2)).ToList();
        foreach (var x in members.Skip(cores.Count)) Add("sup", tag, x.P.Id, Engine.SupportReason(x, cores));
    }

    static List<(string, List<string>, Mon)> Opponents()
    {
        var list = new List<(string, List<string>, Mon)>
        {
            ("water", new() { "water" }, null),
            ("fire-flying", new() { "fire", "flying" }, null),
            ("ghost-dark", new() { "ghost", "dark" }, null),
        };
        foreach (var name in new[] { "Gyarados", "Garchomp", "Shuckle" })
        {
            var m = Dex.Find(name);
            list.Add((name, m.Types.ToList(), m));
        }
        return list;
    }

    static List<(string, BoxConfig)> Configs()
    {
        var list = Engine.Boxes.Select(b => (b.Key, BoxConfig.With(b.Key))).ToList();
        list.Add(("syn+bal", BoxConfig.With("synergy", "balanced")));
        list.Add(("comp+fast", BoxConfig.With("competitive", "fast")));
        list.Add(("tr+legends", BoxConfig.With("trickroom", "legends")));
        list.Add(("wish+syn", BoxConfig.With("wishOnly", "synergy")));
        foreach (var w in new[] { "rain", "sun", "sand", "snow" })
        {
            var c = BoxConfig.With("weather");
            c.WeatherKind = w;
            list.Add(("weather-" + w, c));
        }
        foreach (var type in new[] { "water", "dragon", "steel" })
        {
            var c = BoxConfig.With("mono", "synergy");
            c.MonoType = type;
            list.Add(("mono-" + type, c));
        }
        foreach (var gen in new[] { 1, 4, 9 })
        {
            var c = BoxConfig.With("gen");
            c.GenNum = gen;
            list.Add(("gen-" + gen, c));
        }
        return list;
    }

    static List<List<TeamSlot>> Teams()
    {
        var teams = new List<List<TeamSlot>> { new AppState().Team, new() };
        int n = Dex.All.Count;
        for (int k = 0; k < 12; k++)
        {
            int size = 1 + k % 6;
            var t = new List<TeamSlot>();
            for (int j = 0; j < size; j++)
            {
                var p = Dex.All[(k * 37 + j * 131 + 11) % n];
                var ab = (k + j) % 3 == 0 ? p.Abilities.Last().TrimStart('*') : null;
                t.Add(new TeamSlot { Id = p.Id, Lv = 5 + (k * 13 + j * 7) % 95, Ab = ab });
            }
            teams.Add(t);
        }
        return teams;
    }

    // A saved team as the previous Android app wrote it, to prove updating keeps it.
    static AppState SavedState()
    {
        var gen = BoxConfig.With("competitive", "weather");
        gen.WeatherKind = "sun";
        gen.GenNum = 4;
        return new AppState
        {
            Team = new() { new TeamSlot { Id = 445, Lv = 61, Ab = "Rough Skin" }, new TeamSlot { Id = 6, Lv = 36 } },
            Wish = new() { 282 },
            Tms = false,
            Finals = false,
            Tab = "batalha",
            Opp = new Opponent { Mode = "mon", Mon = "Gyarados" },
            Gen = gen,
            Mode = new BattleMode { Preset = "vgc", Format = "doubles", Size = 6, Pick = 4, Level = "50", BanLegends = true },
        };
    }
}

using PaletMon.Data;
using static PaletMon.Logic.TypeChart;

namespace PaletMon.Logic;

// A Pokémon in a team (current or generated). Text fields use **bold** markup, rendered by the UI.
public sealed class Member
{
    public Mon P;
    public string Ab;
    public int Lv;
    public TeamSlot Slot;
    public bool Core, Kept, Filler;
    public List<PMove> Moves;
    double[] dv; string dvAb;

    public double[] Dv
    {
        get
        {
            if (dv == null || dvAb != Ab) { dvAb = Ab; dv = All.Select(t => Engine.DefMult(P, Ab, t)).ToArray(); }
            return dv;
        }
    }
}

public sealed class PMove
{
    public MoveInfo Info;
    public string Src;
    public double Value;
    public string Name => Info.Name;
    public string Type => Info.Type;
}

public sealed class DefRow
{
    public string Atk;
    public double[] Mults;
    public int Weak, Res;
}

public sealed class TeamScore
{
    public double Score, DefPen, DupPen, AvgBst;
    public List<string> Covered, Holes;
    public List<DefRow> Table;
    public Dictionary<string, int> TypeCount;
}

public sealed class GenResult
{
    public List<Member> Team;
    public double Score;
    public int PoolSize;
    public string Key;
    public TeamScore Sc;
}

public sealed record Alert(string Kind, string Text);
public sealed record Role(string Name, string Tip);
public sealed record Feat(int Spread, bool Fake, bool Redirect, bool Speed, bool Eq);
public sealed record BoxDef(string Key, string Title, string Desc, string Extra = null);
public sealed record Preset(string Label, string Format, int Size, int Pick, string Level, bool BanLegends, bool SleepClause);
public sealed record Weather(string Label, string[] Setters, string[] Abusers, string[] Good, string[] Bad);
public sealed record Ranked(Member X, PMove Best, double Mult, double Incoming, string Speed, double Score);

public static class Engine
{
    public static AppState S;
    static readonly Random Rng = new();

    // ---------- constants ----------
    static readonly string[] SetupP = { "Swords Dance", "Dragon Dance", "Bulk Up", "Victory Dance", "Shell Smash", "Belly Drum", "Hone Claws", "Shift Gear", "Coil" };
    static readonly string[] SetupS = { "Nasty Plot", "Quiver Dance", "Calm Mind", "Shell Smash", "Work Up", "Growth", "Tidy Up" };
    static readonly HashSet<string> StatDrop = new() { "Overheat", "Draco Meteor", "Leaf Storm", "Psycho Boost", "Fleur Cannon", "Make It Rain", "Superpower", "Close Combat", "Hammer Arm", "Ice Hammer", "V Create", "Headlong Rush", "Armor Cannon", "Outrage", "Petal Dance", "Thrash", "Raging Fury" };
    static readonly HashSet<string> SleepMoves = new() { "Spore", "Sleep Powder", "Hypnosis", "Yawn", "Sing", "Lovely Kiss", "Dark Void", "Grass Whistle" };
    static readonly HashSet<string> Spread = new() { "Earthquake", "Rock Slide", "Heat Wave", "Dazzling Gleam", "Hyper Voice", "Surf", "Blizzard", "Discharge", "Muddy Water",
        "Icy Wind", "Snarl", "Eruption", "Water Spout", "Make It Rain", "Bleakwind Storm", "Sandsear Storm", "Wildbolt Storm", "Springtide Storm",
        "Electroweb", "Lava Plume", "Sludge Wave", "Boomburst", "Bulldoze", "Breaking Swipe", "Glacial Lance", "Astral Barrage", "Diamond Storm",
        "Petal Blizzard", "Parabolic Charge", "Mortal Spin", "Air Cutter", "Struggle Bug", "Precipice Blades", "Origin Pulse", "Matcha Gotcha" };
    static readonly string[] Redirect = { "Follow Me", "Rage Powder" };
    static readonly string[] SpeedCtrl = { "Tailwind", "Trick Room", "Icy Wind", "Electroweb", "Thunder Wave" };

    public static readonly Dictionary<string, Preset> Presets = new()
    {
        ["livre"] = new("Livre · Singles 6x6", "singles", 6, 6, "free", false, false),
        ["comp"] = new("Singles competitivo 6x6 · Nv 100", "singles", 6, 6, "100", true, true),
        ["bss"] = new("Singles 3 de 6 · Nv 50", "singles", 6, 3, "50", true, false),
        ["x3"] = new("Singles 3x3", "singles", 3, 3, "free", false, false),
        ["x1"] = new("X1 · 1 contra 1", "singles", 1, 1, "free", false, false),
        ["vgc"] = new("Duplas 4 de 6 · Nv 50 (estilo VGC)", "doubles", 6, 4, "50", true, false),
        ["dbl6"] = new("Duplas 6x6", "doubles", 6, 6, "free", false, false),
        ["tpl6"] = new("Triplas 6x6", "triples", 6, 6, "free", false, false),
        ["lc"] = new("Little Cup · Nv 5, só 1º estágio", "singles", 6, 6, "lc", true, true),
    };
    public static readonly Dictionary<string, string> FormatLabel = new() { ["singles"] = "Singles", ["doubles"] = "Duplas", ["triples"] = "Triplas" };
    public static readonly Dictionary<string, string> LevelLabel = new() { ["free"] = "Nível do jogo", ["50"] = "Nivelado no 50", ["100"] = "Nivelado no 100", ["lc"] = "Little Cup (Nv 5)" };
    public static readonly Dictionary<string, string[]> FormatTips = new()
    {
        ["singles"] = new[] { "Um Pokémon de cada lado. Trocar na hora certa vale mais que qualquer golpe.",
            "Golpes de setup (Swords Dance, Nasty Plot) rendem muito quando o adversário em campo não ameaça você.",
            "Tenha uma resposta para cada tipo que acerta seu time em cheio." },
        ["doubles"] = new[] { "Golpes que acertam os dois adversários (Earthquake, Rock Slide, Heat Wave, Dazzling Gleam) causam 75% em cada alvo e valem muito.",
            "Protect em quase todo mundo: um se protege enquanto o aliado resolve.",
            "Fake Out faz o alvo perder a vez no primeiro turno em campo.",
            "Tailwind e Trick Room decidem quem age primeiro por alguns turnos.",
            "Intimidate baixa o Ataque dos dois adversários ao entrar.",
            "Earthquake e Surf também acertam seu aliado. Combine com parceiros voadores, com Levitate ou que resistam." },
        ["triples"] = new[] { "Três de cada lado. O Pokémon do meio alcança todos; os das pontas só alcançam o lado mais perto com golpes de alvo único.",
            "Golpes em área valem ainda mais do que em duplas.",
            "Deixe o suporte (Follow Me, Wide Guard, Helping Hand) no meio e os atacantes nas pontas." },
    };

    public static readonly Dictionary<string, Weather> Weathers = new()
    {
        ["rain"] = new("Chuva", new[] { "Drizzle" }, new[] { "Swift Swim", "Rain Dish", "Hydration", "Dry Skin" }, new[] { "water", "electric" }, new[] { "fire" }),
        ["sun"] = new("Sol", new[] { "Drought" }, new[] { "Chlorophyll", "Solar Power", "Flower Gift", "Leaf Guard", "Protosynthesis", "Harvest" }, new[] { "fire", "grass" }, new[] { "water" }),
        ["sand"] = new("Areia", new[] { "Sand Stream", "Sand Spit" }, new[] { "Sand Rush", "Sand Force", "Sand Veil" }, new[] { "rock", "ground", "steel" }, new string[0]),
        ["snow"] = new("Neve", new[] { "Snow Warning" }, new[] { "Slush Rush", "Ice Body", "Snow Cloak", "Ice Face" }, new[] { "ice" }, new string[0]),
    };

    static readonly HashSet<string> Meta = new() { "Great Tusk", "Iron Valiant", "Roaring Moon", "Kingambit", "Gholdengo", "Dragapult", "Garchomp", "Corviknight", "Toxapex", "Ferrothorn",
        "Tyranitar", "Volcarona", "Dragonite", "Gliscor", "Clefable", "Skeledirge", "Excadrill", "Scizor", "Weavile", "Gengar", "Azumarill", "Primarina", "Slowbro",
        "Slowking", "Hatterene", "Grimmsnarl", "Cinderace", "Rillaboom", "Pelipper", "Barraskewda", "Hippowdon", "Mamoswine", "Lucario", "Metagross", "Salamence",
        "Hydreigon", "Haxorus", "Baxcalibur", "Annihilape", "Meowscarada", "Quaquaval", "Garganacl", "Amoonguss", "Blissey", "Tinkaton", "Ceruledge", "Armarouge",
        "Palafin", "Kingdra", "Magnezone", "Breloom", "Conkeldurr", "Hawlucha", "Talonflame", "Torkoal", "Ninetales", "Venusaur", "Charizard", "Swampert", "Mimikyu",
        "Toxtricity", "Dondozo", "Glimmora", "Cyclizar", "Espathra", "Clodsire", "Iron Moth", "Iron Treads", "Iron Hands", "Flutter Mane", "Sandy Shocks",
        "Scream Tail", "Brute Bonnet", "Slither Wing", "Iron Bundle", "Alomomola", "Lokix", "Sneasler", "Ursaluna", "Kleavor", "Basculegion", "Polteageist",
        "Gyarados", "Archaludon", "Hydrapple", "Gouging Fire", "Raging Bolt", "Iron Crown", "Iron Boulder", "Sinistcha", "Heatran", "Landorus", "Zapdos",
        "Latios", "Ting Lu", "Chien Pao", "Ogerpon", "Ninetales Alola", "Arcanine Hisui", "Samurott Hisui", "Goodra Hisui", "Zoroark Hisui", "Weezing Galar", "Slowking Galar" };

    public static readonly BoxDef[] Boxes =
    {
        new("random", "Aleatório", "Cada clique sorteia um time diferente. Sozinho, gera um time 100% aleatório."),
        new("synergy", "Sinergia", "Um cobre a fraqueza do outro e o time acerta o máximo de tipos."),
        new("competitive", "Competitivo", "Pokémon fortes do cenário competitivo, atributos altos e papéis claros."),
        new("balanced", "Equilibrado", "Mistura atacantes físicos, especiais e pelo menos um tanque."),
        new("fast", "Rápido", "Prioriza Pokémon com velocidade alta para atacar primeiro."),
        new("trickroom", "Trick Room", "Pokémon lentos e fortes, com alguém que aprende Trick Room."),
        new("weather", "Time de clima", "Um Pokémon que liga o clima e outros que se beneficiam dele.", "weatherKind"),
        new("mono", "Monotipo", "Todos os Pokémon compartilham um tipo.", "monoType"),
        new("gen", "Uma geração", "Só Pokémon de uma geração.", "genNum"),
        new("noDupTypes", "Sem tipos repetidos", "Nenhum tipo aparece duas vezes."),
        new("keepTeam", "Manter meu time atual", "Os Pokémon do seu time ficam e o gerador completa o resto."),
        new("wishPrio", "Priorizar minha lista", "Dá preferência aos Pokémon da aba Montar time."),
        new("wishOnly", "Só da minha lista", "Usa apenas os Pokémon da sua lista (nas formas finais)."),
        new("legends", "Permitir lendários", "Inclui lendários e míticos."),
    };
    public static readonly string[] BuildKeys = { "synergy", "competitive", "balanced", "random", "fast", "trickroom", "weather", "mono", "gen", "noDupTypes", "legends" };

    // ---------- evolution ----------
    static readonly Dictionary<int, List<Mon>> evoCache = new();
    static string RegionOf(Mon p)
    {
        if (!p.Regional) return null;
        foreach (var r in new[] { "Alola", "Galar", "Hisui", "Paldea" }) if (p.Name.Contains(r)) return r;
        return null;
    }
    public static List<Mon> NextEvos(Mon p)
    {
        if (evoCache.TryGetValue(p.Id, out var c)) return c;
        var kids = Dex.All.Where(q => q.From == p.Species && q.Species != p.Species).ToList();
        var reg = RegionOf(p);
        List<Mon> res;
        if (reg != null)
        {
            var same = kids.Where(q => RegionOf(q) == reg).ToList();
            res = same.Count > 0 ? same : kids.Where(q => !q.Regional).ToList();
        }
        else res = kids.Where(q => !q.Regional).ToList();
        evoCache[p.Id] = res;
        return res;
    }
    public static List<Mon> FinalForms(Mon p, HashSet<int> seen = null)
    {
        seen ??= new();
        if (!seen.Add(p.Id)) return new() { p };
        var n = NextEvos(p);
        return n.Count == 0 ? new() { p } : n.SelectMany(q => FinalForms(q, seen)).ToList();
    }
    public static bool IsFinal(Mon p) => NextEvos(p).Count == 0;
    public static bool IsBasic(Mon p) => p.From == 0 && NextEvos(p).Count > 0;
    public static string EvoText(Evo e)
    {
        if (e == null) return "";
        if (e.Level > 0) return $"nível {e.Level}" + (e.Time == "night" ? " (à noite)" : e.Time == "day" ? " (de dia)" : e.Time != null ? $" ({e.Time})" : "");
        if (e.Item != null) return "usando " + e.Item;
        return e.How;
    }

    // ---------- members ----------
    public static IEnumerable<string> AbilityNames(Mon p) => p.Abilities.Select(a => a.TrimStart('*'));
    public static string DefaultAbility(Mon p)
    {
        var regular = p.Abilities.FirstOrDefault(a => !a.StartsWith("*"));
        return (regular ?? p.Abilities.FirstOrDefault() ?? "").TrimStart('*');
    }
    public static double DefMult(Mon p, string ability, string atk)
    {
        double m = 1;
        foreach (var t in p.Types) m *= Eff(atk, t);
        if (ability != null && AbilityDef.TryGetValue(ability, out var mod) && mod.TryGetValue(atk, out var v)) m = v == 0 ? 0 : m * v;
        return m;
    }
    public static List<Member> ResolveTeam() => S.Team.Select(slot =>
    {
        var p = Dex.ById[slot.Id];
        var ab = slot.Ab != null && AbilityNames(p).Contains(slot.Ab) ? slot.Ab : DefaultAbility(p);
        return new Member { P = p, Ab = ab, Lv = slot.Lv, Slot = slot };
    }).ToList();
    public static Member Cand(Mon p, string ab = null) => new() { P = p, Ab = ab ?? DefaultAbility(p) };

    public static char StyleOf(Mon p)
    {
        int atk = p.Stats[1], spa = p.Stats[3];
        if (atk >= spa * 1.15) return 'P';
        if (spa >= atk * 1.15) return 'S';
        return 'M';
    }
    public static Role RoleOf(Mon p)
    {
        var b = p.Stats;
        var style = StyleOf(p);
        int bulk = b[0] + b[2] + b[4], offense = Math.Max(b[1], b[3]), spe = b[5];
        var kind = style == 'P' ? "físico" : style == 'S' ? "especial" : "misto";
        if (spe >= 95 && offense >= 95) return new($"Atacante {kind} rápido", "Ataque primeiro e derrube antes de levar dano. Golpes de setup aumentam muito o estrago.");
        if (bulk >= 270 && offense < 95) return new("Tanque / suporte", "Aguenta golpes. Use status (queimar, envenenar, paralisar) e recuperação.");
        if (bulk >= 260) return new($"Atacante {kind} resistente", "Pode entrar em campo levando um golpe e revidar com força.");
        if (spe < 60 && offense >= 95) return new($"Atacante {kind} lento", "Bate forte mas age por último. Entre depois que um aliado cair ou contra alvos lentos.");
        if (offense >= 85) return new($"Atacante {kind}", "Foque em golpes STAB fortes e um golpe de cobertura.");
        return new("Em desenvolvimento", "Atributos baixos ainda. Vale evoluir antes de confiar nele em lutas difíceis.");
    }

    // ---------- battle mode ----------
    public static bool IsMulti => S.Mode.Format != "singles";
    public static int ActiveCount => S.Mode.Format == "doubles" ? 2 : S.Mode.Format == "triples" ? 3 : 1;
    public static int MaxCore => Math.Max(1, Math.Min(5, S.Mode.Size - 1));
    public static int BattleLv(int lv) => S.Mode.Level switch { "50" => 50, "100" => 100, "lc" => 5, _ => lv };
    public static string ModeSummary()
    {
        var m = S.Mode;
        var size = m.Pick < m.Size ? $"{m.Pick} de {m.Size}" : $"{m.Size}x{m.Size}";
        return $"{FormatLabel[m.Format]} {size} · {LevelLabel[m.Level]}" + (m.BanLegends ? " · sem lendários" : "") + (m.SleepClause ? " · sem sono" : "");
    }
    public static void ApplyPreset(string key)
    {
        var p = Presets[key];
        S.Mode = new BattleMode { Preset = key, Format = p.Format, Size = p.Size, Pick = p.Pick, Level = p.Level, BanLegends = p.BanLegends, SleepClause = p.SleepClause };
    }

    static readonly Dictionary<int, HashSet<string>> namesCache = new();
    public static HashSet<string> MoveNames(Mon p)
    {
        if (namesCache.TryGetValue(p.Id, out var c)) return c;
        var ids = p.OtherMoves.Concat(p.LevelMoves.Where((_, i) => i % 2 == 0));
        c = new HashSet<string>(ids.Where(Dex.Moves.ContainsKey).Select(id => Dex.Moves[id].Name));
        namesCache[p.Id] = c;
        return c;
    }
    static readonly Dictionary<int, Feat> featCache = new();
    public static Feat FeatOf(Mon p)
    {
        if (featCache.TryGetValue(p.Id, out var f)) return f;
        var n = MoveNames(p);
        f = new Feat(n.Count(Spread.Contains), n.Contains("Fake Out"), Redirect.Any(n.Contains), SpeedCtrl.Any(n.Contains), n.Contains("Earthquake"));
        featCache[p.Id] = f;
        return f;
    }

    // ---------- moves ----------
    public static List<PMove> Learnable(Mon p, int? lv, bool tms)
    {
        var res = new List<PMove>();
        for (int i = 0; i + 1 < p.LevelMoves.Length; i += 2)
        {
            int id = p.LevelMoves[i], at = p.LevelMoves[i + 1];
            if (lv == null || at <= lv) Add(id, at <= 1 ? "Nv 1" : "Nv " + at);
        }
        if (tms) foreach (var id in p.OtherMoves) Add(id, "TM/tutor/ovo");
        return res;

        void Add(int id, string src)
        {
            if (!Dex.Moves.TryGetValue(id, out var info)) return;
            if (info.Name == "Tera Blast") return;
            if (S.Mode.SleepClause && SleepMoves.Contains(info.Name)) return;
            if (info.Name == "Fake Out" && !IsMulti) return;
            res.Add(new PMove { Info = info, Src = src });
        }
    }
    public static double MoveValue(Mon p, PMove mv)
    {
        var i = mv.Info;
        if (i.Class == 1) return 0;
        double pw = i.Power == -1 ? 65 : i.Power;
        double acc = i.Accuracy > 100 ? 1 : i.Accuracy / 100.0;
        double stab = p.Types.Contains(i.Type) ? 1.5 : 1;
        double stat = i.Class == 2 ? p.Stats[1] : p.Stats[3];
        double best = Math.Max(p.Stats[1], p.Stats[3]);
        double drawback = StatDrop.Contains(i.Name) ? 0.75 : 1;
        double spread = IsMulti && Spread.Contains(i.Name) ? 1.3 : 1;
        return pw * acc * stab * (stat / best) * (i.Priority > 0 ? 0.9 : 1) * drawback * spread;
    }
    public static List<PMove> PickMoves(Mon p, int? lv, bool tms)
    {
        var pool = Learnable(p, lv, tms);
        var atks = pool.Where(m => m.Info.Class != 1 && (m.Info.Power == -1 || m.Info.Power >= 40)).ToList();
        foreach (var m in atks) m.Value = MoveValue(p, m);
        var chosen = new List<PMove>();
        void Take(PMove m) { if (m != null && !chosen.Any(c => c.Info.Id == m.Info.Id)) chosen.Add(m); }

        foreach (var t in p.Types) Take(atks.Where(m => m.Type == t).OrderByDescending(m => m.Value).FirstOrDefault());
        var style = StyleOf(p);
        var setupList = style == 'P' ? SetupP : style == 'S' ? SetupS : SetupP.Concat(SetupS).ToArray();
        var setup = setupList.Select(n => pool.FirstOrDefault(m => m.Name == n)).FirstOrDefault(m => m != null);
        int CoverSlots() => 4 - chosen.Count - (setup != null && !chosen.Contains(setup) ? 1 : 0);

        var hits = new HashSet<string>();
        void MarkHits(PMove m) { foreach (var d in All) if (Eff(m.Type, d) >= 2) hits.Add(d); }
        chosen.ForEach(MarkHits);
        while (CoverSlots() > 0)
        {
            PMove best = null; double bestScore = 0;
            foreach (var m in atks)
            {
                if (chosen.Any(c => c.Info.Id == m.Info.Id || c.Type == m.Type)) continue;
                int fresh = All.Count(d => Eff(m.Type, d) >= 2 && !hits.Contains(d));
                double s = fresh * 30 + m.Value;
                if (fresh > 0 && s > bestScore) { best = m; bestScore = s; }
            }
            if (best == null) break;
            Take(best); MarkHits(best);
        }
        if (setup != null) Take(setup);
        var rest = atks.OrderByDescending(m => m.Value).ToList();
        foreach (var m in rest) { if (chosen.Count >= 4) break; if (!chosen.Any(c => c.Type == m.Type)) Take(m); }
        foreach (var m in rest) { if (chosen.Count >= 4) break; if (chosen.Count(c => c.Type == m.Type) < 2) Take(m); }
        if (!IsMulti) return chosen.Take(4).ToList();

        // doubles/triples: two attacks, one support move, Protect
        PMove Has(string n) => pool.FirstOrDefault(m => m.Name == n);
        var utilOrder = p.Stats[5] <= 60
            ? new[] { "Trick Room", "Follow Me", "Rage Powder", "Fake Out", "Helping Hand", "Icy Wind", "Electroweb", "Wide Guard", "Tailwind" }
            : new[] { "Fake Out", "Tailwind", "Follow Me", "Rage Powder", "Icy Wind", "Electroweb", "Snarl", "Helping Hand", "Wide Guard" };
        var outList = chosen.Where(m => m.Info.Class != 1 && p.Types.Contains(m.Type)).Take(2).ToList();
        var util = utilOrder.Select(Has).FirstOrDefault(m => m != null && !outList.Contains(m));
        if (util != null) outList.Add(util);
        var protect = Has("Protect");
        if (protect != null) outList.Add(protect);
        foreach (var m in chosen.Concat(rest))
        {
            if (outList.Count >= 4) break;
            if (m.Info.Class != 1 && !outList.Contains(m) && !outList.Any(c => c.Type == m.Type)) outList.Add(m);
        }
        return outList.Take(4).ToList();
    }

    // ---------- team analysis ----------
    public static List<DefRow> DefenseTable(List<Member> members) => All.Select((atk, ai) =>
    {
        var mults = members.Select(x => x.Dv[ai]).ToArray();
        return new DefRow { Atk = atk, Mults = mults, Weak = mults.Count(v => v > 1), Res = mults.Count(v => v < 1) };
    }).ToList();

    public static TeamScore ScoreTeam(List<Member> members)
    {
        var table = DefenseTable(members);
        double defPen = 0; var holes = new List<string>();
        foreach (var r in table)
        {
            int gap = r.Weak - r.Res;
            if (gap > 0) defPen += gap * gap * 3;
            if (r.Weak >= 3) defPen += 6;
            if (r.Weak >= 2 && r.Res == 0) holes.Add(r.Atk);
        }
        var stab = members.SelectMany(x => x.P.Types).Distinct().ToList();
        var covered = All.Where(d => stab.Any(t => Eff(t, d) >= 2)).ToList();
        var typeCount = new Dictionary<string, int>();
        foreach (var x in members) foreach (var t in x.P.Types) typeCount[t] = typeCount.GetValueOrDefault(t) + 1;
        double dupPen = typeCount.Values.Sum(n => n > 1 ? (n - 1) * (n - 1) * 5 : 0);
        double avgBst = members.Count == 0 ? 0 : members.Average(x => x.P.Bst);
        var styles = members.Select(x => StyleOf(x.P)).ToList();
        double balance = (styles.Contains('P') || styles.Contains('M')) && (styles.Contains('S') || styles.Contains('M')) ? 6 : 0;
        int fast = members.Count(x => x.P.Stats[5] >= 90);
        double score = covered.Count * 4 - defPen - dupPen + avgBst / 10 + balance + Math.Min(fast, 2) * 3;
        return new TeamScore { Score = score, Covered = covered, Holes = holes, DefPen = defPen, DupPen = dupPen, AvgBst = avgBst, Table = table, TypeCount = typeCount };
    }

    public static IEnumerable<List<T>> KCombos<T>(IList<T> arr, int k, int start = 0, List<T> acc = null)
    {
        acc ??= new();
        if (acc.Count == k) { yield return acc.ToList(); yield break; }
        for (int i = start; i <= arr.Count - (k - acc.Count); i++)
        {
            acc.Add(arr[i]);
            foreach (var c in KCombos(arr, k, i + 1, acc)) yield return c;
            acc.RemoveAt(acc.Count - 1);
        }
    }
    public static List<Member> BestSubset(List<Member> members, int k)
    {
        if (members.Count <= k) return members;
        List<Member> best = null; double bestS = double.MinValue;
        foreach (var c in KCombos(members, k))
        {
            var sc = ScoreTeam(c);
            var s = sc.Score + sc.AvgBst / 5;
            if (s > bestS) { bestS = s; best = c; }
        }
        return best;
    }

    public static string Names(IEnumerable<Member> xs) => string.Join(", ", xs.Select(x => $"**{x.P.Name}**"));
    static string PtList(IEnumerable<string> types) => string.Join(", ", types.Select(t => Pt[t]));

    public static List<Alert> TeamAlerts(List<Member> members, TeamScore sc)
    {
        var alerts = new List<Alert>();
        foreach (var x in members)
        {
            var ready = NextEvos(x.P).Where(k => k.Evo != null && k.Evo.Level > 0 && x.Lv >= k.Evo.Level).ToList();
            if (ready.Count > 0) alerts.Add(new("good", $"**{x.P.Name}** já pode evoluir para **{string.Join(" / ", ready.Select(k => k.Name))}** (nível {ready[0].Evo.Level}). Abra o menu do time no jogo para confirmar a evolução."));
            var items = NextEvos(x.P).Where(k => k.Evo?.Item != null).ToList();
            if (items.Count > 0) alerts.Add(new("neutral", $"**{x.P.Name}** evolui com item: {string.Join(" ou ", items.Select(k => $"{k.Evo.Item} → **{k.Name}**"))}."));
        }
        foreach (var (t, n) in sc.TypeCount) if (n >= 3) alerts.Add(new("bad", $"{n} Pokémon do tipo {Pt[t]} no time. Isso concentra as mesmas fraquezas: {PtList(All.Where(a => Eff(a, t) >= 2))}."));
        foreach (var t in sc.Holes)
        {
            var r = sc.Table.First(x => x.Atk == t);
            alerts.Add(new(r.Weak >= 3 ? "bad" : "warn", $"Golpes {Pt[t]} acertam {r.Weak} do time em cheio e ninguém resiste."));
        }
        if (members.Count == 0) alerts.Add(new("warn", "Seu time está vazio. Adicione Pokémon abaixo."));

        var md = S.Mode;
        if (members.Count > md.Size) alerts.Add(new("bad", $"O modo {ModeSummary()} usa {md.Size} Pokémon e seu time tem {members.Count}. Remova {members.Count - md.Size}."));
        else if (members.Count > 0 && members.Count < md.Size) alerts.Add(new("warn", $"O modo pede {md.Size} Pokémon e seu time tem {members.Count}."));
        var banned = members.Where(x => md.BanLegends && (x.P.Legendary || x.P.Mythical)).ToList();
        if (banned.Count > 0) alerts.Add(new("bad", $"Lendários e míticos estão proibidos neste modo: {Names(banned)}."));
        if (md.Level == "lc")
        {
            var notBasic = members.Where(x => !IsBasic(x.P)).ToList();
            if (notBasic.Count > 0) alerts.Add(new("bad", $"Little Cup só aceita Pokémon no 1º estágio que ainda evoluem: {Names(notBasic)} não vale."));
        }
        if (md.Level != "free") alerts.Add(new("neutral", $"{LevelLabel[md.Level]}: os golpes sugeridos consideram o nível {BattleLv(1)}."));
        if (md.Pick < md.Size && members.Count > md.Pick) alerts.Add(new("neutral", $"Você leva {md.Pick} de {md.Size}. Melhor grupo geral: {Names(BestSubset(members, md.Pick))}. Na aba Batalha dá para escolher contra um adversário específico."));
        if (IsMulti && members.Count > 0)
        {
            var lacks = new List<string>();
            if (!members.Any(x => FeatOf(x.P).Fake)) lacks.Add("Fake Out");
            if (!members.Any(x => FeatOf(x.P).Speed)) lacks.Add("controle de velocidade (Tailwind, Trick Room, Icy Wind)");
            if (!members.Any(x => FeatOf(x.P).Redirect)) lacks.Add("alguém com Follow Me ou Rage Powder");
            if (!members.Any(x => x.Ab == "Intimidate")) lacks.Add("Intimidate");
            var f = FormatLabel[md.Format].ToLowerInvariant();
            alerts.Add(lacks.Count > 0 ? new("warn", $"Para {f}, falta no time: {string.Join(", ", lacks)}.")
                : new("good", $"Time pronto para {f}: tem Fake Out, controle de velocidade, redirecionamento e Intimidate."));
        }
        return alerts;
    }

    // best suggested move per member vs each defending type
    public static List<(string Type, double Best, List<string> Who)> Offense(List<Member> members) => All.Select(d =>
    {
        double best = 0; var who = new List<string>();
        foreach (var x in members)
        {
            var m = x.Moves.Where(z => z.Info.Class != 1).Select(z => Eff(z.Type, d)).DefaultIfEmpty(0).Max();
            if (m > best) { best = m; who = new() { x.P.Name }; }
            else if (m == best && m >= 2) who.Add(x.P.Name);
        }
        return (d, best, who);
    }).ToList();

    public static List<(Mon P, string Why)> Suggestions(List<Member> members, int n)
    {
        var baseScore = ScoreTeam(members).Score;
        var have = members.Select(x => x.P.Species).ToHashSet();
        var outList = new List<(Mon p, double gain, string replace)>();
        foreach (var p in Dex.All)
        {
            if (!IsFinal(p) || have.Contains(p.Species) || p.Bst < 420) continue;
            if ((!S.Legends || S.Mode.BanLegends) && (p.Legendary || p.Mythical)) continue;
            var cand = Cand(p);
            double gain; string replace = null;
            if (members.Count < 6) gain = ScoreTeam(members.Append(cand).ToList()).Score - baseScore;
            else
            {
                gain = double.MinValue;
                for (int i = 0; i < members.Count; i++)
                {
                    var g = ScoreTeam(members.Select((y, j) => j == i ? cand : y).ToList()).Score - baseScore;
                    if (g > gain) { gain = g; replace = members[i].P.Name; }
                }
            }
            outList.Add((p, gain, replace));
        }
        var holes = ScoreTeam(members).Holes;
        return outList.OrderByDescending(o => o.gain).Take(n).Select(o =>
        {
            var parts = new List<string>();
            var resists = holes.Where(t => DefMult(o.p, DefaultAbility(o.p), t) < 1).ToList();
            if (resists.Count > 0) parts.Add("resiste a " + PtList(resists));
            var newHits = All.Where(d => o.p.Types.Any(t => Eff(t, d) >= 2) && !members.Any(x => x.P.Types.Any(t => Eff(t, d) >= 2))).ToList();
            if (newHits.Count > 0) parts.Add("STAB novo contra " + PtList(newHits));
            parts.Add($"BST {o.p.Bst}");
            if (o.replace != null) parts.Add("no lugar de " + o.replace);
            var pre = Dex.All.FirstOrDefault(q => q.Chain == o.p.Chain && q.From == 0 && !q.Regional);
            if (pre != null && pre.Species != o.p.Species) parts.Add("começa como " + pre.Name);
            return (o.p, string.Join(" · ", parts));
        }).ToList();
    }

    // ---------- box generator ----------
    public static List<Mon> GenPool(BoxConfig cfg)
    {
        bool lc = S.Mode.Level == "lc";
        IEnumerable<Mon> pool = cfg["wishOnly"]
            ? S.Wish.SelectMany(id => lc ? new List<Mon> { Dex.ById[id] } : FinalForms(Dex.ById[id])).GroupBy(p => p.Id).Select(g => g.First())
            : Dex.All.Where(p => (lc ? IsBasic(p) : IsFinal(p)) && p.LevelMoves.Length + p.OtherMoves.Length > 0);
        return pool.Where(p =>
        {
            if ((!cfg["legends"] || S.Mode.BanLegends) && (p.Legendary || p.Mythical)) return false;
            if (lc && !IsBasic(p)) return false;
            if (cfg["mono"] && !p.Types.Contains(cfg.MonoType)) return false;
            if (cfg["gen"] && p.Gen != cfg.GenNum) return false;
            if (cfg["competitive"] && !cfg["wishOnly"] && !lc && p.Bst < 440) return false;
            return true;
        }).ToList();
    }
    public static string BestAbility(Mon p, BoxConfig cfg)
    {
        var names = AbilityNames(p).ToList();
        if (cfg["weather"])
        {
            var w = Weathers[cfg.WeatherKind];
            var hit = names.FirstOrDefault(a => w.Setters.Contains(a)) ?? names.FirstOrDefault(a => w.Abusers.Contains(a));
            if (hit != null) return hit;
        }
        if (IsMulti && names.Contains("Intimidate")) return "Intimidate";
        return DefaultAbility(p);
    }
    static bool ScoringOn(BoxConfig cfg) => new[] { "synergy", "competitive", "balanced", "fast", "trickroom", "weather", "wishPrio", "noDupTypes" }.Any(k => cfg[k]);
    static bool LearnsMove(Mon p, string name) => MoveNames(p).Contains(name);

    public static double GenScore(List<Member> members, BoxConfig cfg)
    {
        var sc = ScoreTeam(members);
        double s = 0;
        double dup = sc.TypeCount.Values.Sum(n => n > 1 ? (n - 1) * (n - 1) : 0);
        if (!cfg["mono"]) s -= dup * (cfg["noDupTypes"] ? 40 : 4);
        if (IsMulti)
        {
            bool fake = false, redirect = false, speed = false;
            int gi = Index("ground");
            bool eqUser = members.Any(x => FeatOf(x.P).Eq && x.P.Types.Contains("ground"));
            foreach (var x in members)
            {
                var f = FeatOf(x.P);
                s += Math.Min(f.Spread, 2) * 3;
                if (x.Ab == "Intimidate") s += 6;
                if (x.Ab == "Prankster") s += 3;
                if (x.Ab == "Friend Guard" || x.Ab == "Telepathy") s += 2;
                if (eqUser && x.Dv[gi] == 0) s += 3;
                fake |= f.Fake; redirect |= f.Redirect; speed |= f.Speed;
            }
            s += (fake ? 10 : 0) + (redirect ? 8 : 0) + (speed ? 8 : 0);
        }
        // when you only bring part of the team, each member has to hold up alone
        if (S.Mode.Pick < S.Mode.Size) s += sc.AvgBst / 10;
        if (cfg["synergy"])
        {
            s += sc.Covered.Count * 5 - sc.DefPen * 1.2 - sc.Holes.Count * 6;
            foreach (var a in members) foreach (var b in members)
                {
                    if (a == b) continue;
                    var da = a.Dv; var db = b.Dv;
                    for (int i = 0; i < 18; i++) if (da[i] > 1 && db[i] < 1) s += 0.6;
                }
        }
        else s += sc.Covered.Count * 1.5 - sc.DefPen * 0.3;
        if (cfg["competitive"])
            foreach (var x in members) s += (x.P.Bst - 480) / 8.0 + (Meta.Contains(x.P.Name) ? 14 : 0) + (x.P.Stats.Skip(1).Max() >= 110 ? 3 : 0);
        else s += sc.AvgBst / 8;
        if (cfg["balanced"])
        {
            var st = members.Select(x => StyleOf(x.P)).ToList();
            if (st.Any(v => v != 'S')) s += 6;
            if (st.Any(v => v != 'P')) s += 6;
            if (members.Any(x => x.P.Stats[0] + x.P.Stats[2] + x.P.Stats[4] >= 270)) s += 6;
        }
        if (cfg["fast"]) foreach (var x in members) s += x.P.Stats[5] >= 100 ? 8 : x.P.Stats[5] >= 85 ? 3 : -3;
        if (cfg["trickroom"])
        {
            foreach (var x in members) s += x.P.Stats[5] <= 55 && Math.Max(x.P.Stats[1], x.P.Stats[3]) >= 100 ? 12 : x.P.Stats[5] >= 85 ? -16 : 0;
            if (members.Any(x => LearnsMove(x.P, "Trick Room"))) s += 30;
        }
        if (cfg["weather"])
        {
            var w = Weathers[cfg.WeatherKind];
            if (members.Any(x => w.Setters.Contains(x.Ab))) s += 45;
            foreach (var x in members)
            {
                if (w.Abusers.Contains(x.Ab)) s += 14;
                if (x.P.Types.Any(w.Good.Contains)) s += 6;
                if (x.P.Types.Any(w.Bad.Contains)) s -= 6;
            }
        }
        if (cfg["wishPrio"]) foreach (var x in members) if (S.Wish.Any(id => Dex.ById[id].Chain == x.P.Chain)) s += 15;
        return s;
    }

    public static GenResult GenerateBoxes(BoxConfig cfg, HashSet<string> shown, List<Member> fixedOverride = null)
    {
        var pool = GenPool(cfg).Select(p => Cand(p, BestAbility(p, cfg))).ToList();
        var fixedList = fixedOverride ?? (cfg["keepTeam"] ? ResolveTeam().Select(x => { x.Kept = true; return x; }).ToList() : new List<Member>());
        int N = S.Mode.Size;
        int need = N - fixedList.Count;
        if (need <= 0) return new GenResult { Team = fixedList, Score = GenScore(fixedList, cfg), PoolSize = pool.Count };
        var fixedChains = fixedList.Select(x => x.P.Chain).ToHashSet();
        var free = pool.Where(c => !fixedChains.Contains(c.P.Chain)).ToList();
        if (free.Count == 0) return null;

        if (cfg["random"] && !ScoringOn(cfg))
        {
            var pick = new List<Member>(); var used = new HashSet<int>();
            foreach (var c in free.OrderBy(_ => Rng.Next()))
            {
                if (pick.Count >= need) break;
                if (used.Add(c.P.Chain)) pick.Add(c);
            }
            return new GenResult { Team = fixedList.Concat(pick).ToList(), PoolSize = pool.Count };
        }
        int restarts = cfg["random"] ? 8 : 14, topK = cfg["random"] ? 25 : 3;
        GenResult best = null;
        for (int r = 0; r < restarts; r++)
        {
            var team = fixedList.ToList(); var used = new HashSet<int>(fixedChains);
            bool first = true;
            while (team.Count < N)
            {
                var scored = new List<(Member c, double s)>();
                foreach (var c in free)
                {
                    if (used.Contains(c.P.Chain)) continue;
                    team.Add(c);
                    scored.Add((c, GenScore(team, cfg) + (cfg["random"] ? Rng.NextDouble() * 25 : Rng.NextDouble() * 2)));
                    team.RemoveAt(team.Count - 1);
                }
                if (scored.Count == 0) break;
                scored.Sort((a, b) => b.s.CompareTo(a.s));
                int k = first ? Math.Min(scored.Count, cfg["random"] ? 60 : 20) : Math.Min(scored.Count, topK);
                var choice = scored[Rng.Next(k)].c;
                first = false;
                team.Add(choice); used.Add(choice.P.Chain);
            }
            var key = string.Join(",", team.Select(x => x.P.Id).OrderBy(i => i));
            if (shown.Contains(key) && r < restarts - 1) continue;
            var s = GenScore(team, cfg);
            if (best == null || s > best.Score) best = new GenResult { Team = team, Score = s, Key = key };
        }
        if (best == null) return null;
        shown.Add(best.Key);
        best.PoolSize = pool.Count;
        best.Sc = ScoreTeam(best.Team);
        return best;
    }

    public static List<string> ExplainTeam(List<Member> team, BoxConfig cfg)
    {
        var outList = new List<string>();
        var sc = ScoreTeam(team);
        var md = S.Mode;
        if (team.Count < md.Size) outList.Add($"Só deu para montar {team.Count} Pokémon com essas caixas" + (cfg["wishOnly"] ? $" (sua lista não tem {md.Size} linhas evolutivas diferentes; adicione mais na aba Montar time)" : "") + ".");
        if (md.Pick < md.Size && team.Count >= md.Pick) outList.Add($"Neste modo você leva {md.Pick} de {md.Size}. Melhor grupo geral: {Names(BestSubset(team, md.Pick))}. Ajuste conforme o adversário na aba Batalha.");
        if (IsMulti)
        {
            var parts = new List<string>();
            string Who(Func<Feat, bool> f, int take = 6) => string.Join(", ", team.Where(x => f(FeatOf(x.P))).Take(take).Select(x => x.P.Name));
            var fake = Who(f => f.Fake); var speed = Who(f => f.Speed, 3); var redir = Who(f => f.Redirect);
            if (fake != "") parts.Add("Fake Out: " + fake);
            if (speed != "") parts.Add("controle de velocidade: " + speed);
            if (redir != "") parts.Add("puxa golpes (Follow Me/Rage Powder): " + redir);
            var intim = string.Join(", ", team.Where(x => x.Ab == "Intimidate").Select(x => x.P.Name));
            if (intim != "") parts.Add("Intimidate: " + intim);
            if (parts.Count > 0) outList.Add($"Para {FormatLabel[md.Format].ToLowerInvariant()}: {string.Join(" · ", parts)}.");
        }
        if (cfg["weather"])
        {
            var w = Weathers[cfg.WeatherKind];
            var setter = team.FirstOrDefault(x => w.Setters.Contains(x.Ab));
            var abusers = team.Where(x => w.Abusers.Contains(x.Ab)).ToList();
            outList.Add(setter != null ? $"**{setter.P.Name}** liga {w.Label.ToLowerInvariant()} com {setter.Ab} ao entrar." : $"Nenhum Pokémon da seleção liga {w.Label.ToLowerInvariant()} sozinho. Use o golpe de clima ou tire filtros.");
            if (abusers.Count > 0) outList.Add("Aproveitam o clima: " + string.Join(", ", abusers.Select(x => $"**{x.P.Name}** ({x.Ab})")) + ".");
        }
        if (cfg["trickroom"])
        {
            var setter = team.Where(x => LearnsMove(x.P, "Trick Room")).ToList();
            outList.Add(setter.Count > 0 ? $"Trick Room com {Names(setter)}. Depois dele, os mais lentos atacam primeiro por 5 turnos." : "Ninguém aqui aprende Trick Room. Tire alguns filtros para achar um.");
        }
        if (cfg["synergy"] || cfg["competitive"])
        {
            outList.Add(sc.Holes.Count > 0 ? "Brechas restantes: " + PtList(sc.Holes) + "." : "Nenhum tipo de golpe acerta 2 ou mais do time sem alguém resistir.");
            outList.Add($"Os STABs do time acertam {sc.Covered.Count} de 18 tipos super efetivo.");
        }
        var fast = team.Where(x => x.P.Stats[5] >= 100).Select(x => x.P.Name).ToList();
        if (fast.Count > 0) outList.Add("Mais rápidos: " + string.Join(", ", fast) + ".");
        var tanks = team.Where(x => x.P.Stats[0] + x.P.Stats[2] + x.P.Stats[4] >= 270).Select(x => x.P.Name).ToList();
        if (tanks.Count > 0) outList.Add("Seguram dano: " + string.Join(", ", tanks) + ".");
        return outList;
    }

    // ---------- build around chosen ----------
    public static string SupportReason(Member f, List<Member> cores)
    {
        var parts = new List<string>();
        var fdv = f.Dv;
        var guards = All.Where((t, i) => fdv[i] < 1 && cores.Any(c => c.Dv[i] > 1)).ToList();
        if (guards.Count > 0) parts.Add($"aguenta golpes {PtList(guards)}, que ameaçam {(cores.Count > 1 ? "seus escolhidos" : cores[0].P.Name)}");
        var coreStab = cores.SelectMany(c => c.P.Types).Distinct().ToList();
        var walls = All.Where(d => coreStab.All(t => Eff(t, d) <= 1));
        var breaks = walls.Where(d => f.P.Types.Any(t => Eff(t, d) >= 2)).ToList();
        if (breaks.Count > 0) parts.Add($"acerta em cheio {PtList(breaks)}, que seus escolhidos não acertam");
        return parts.Count > 0 ? string.Join("; ", parts) : "reforça os atributos e o papel que faltava no time";
    }

    public static List<GenResult> BuildAround(HashSet<string> shown)
    {
        var cfg = S.Build;
        var chosen = S.Wish.Take(MaxCore).Select(id => Dex.ById[id]).ToList();
        var options = chosen.Select(p => S.Finals && S.Mode.Level != "lc" ? FinalForms(p) : new List<Mon> { p }).ToList();
        IEnumerable<List<Mon>> combos = new[] { new List<Mon>() };
        foreach (var o in options) combos = combos.SelectMany(a => o.Select(x => a.Append(x).ToList()));
        List<Member> core = null; double coreScore = double.MinValue;
        foreach (var combo in combos.Take(32))
        {
            var fixedList = combo.Select(p => new Member { P = p, Ab = BestAbility(p, cfg), Core = true }).ToList();
            var s = GenScore(fixedList, cfg);
            if (s > coreScore) { coreScore = s; core = fixedList; }
        }
        var results = new List<GenResult>();
        for (int n = 0; n < 3; n++)
        {
            var r = GenerateBoxes(cfg, shown, core);
            if (r == null) break;
            r.Key ??= string.Join(",", r.Team.Select(x => x.P.Id).OrderBy(i => i));
            if (results.Any(x => x.Key == r.Key)) break;
            r.Sc = ScoreTeam(r.Team);
            results.Add(r);
        }
        return results;
    }

    // ---------- battle ----------
    public static List<Ranked> RankVs(List<Member> members, List<string> oppTypes, Mon oppMon)
    {
        return members.Select(x =>
        {
            var mv = PickMoves(x.P, BattleLv(x.Lv), S.Tms).Where(m => m.Info.Class != 1).ToList();
            PMove best = null; double bestDmg = 0, bestMult = 0;
            foreach (var m in mv)
            {
                double mult = oppTypes.Aggregate(1.0, (a, t) => a * Eff(m.Type, t));
                double dmg = MoveValue(x.P, m) * mult;
                if (dmg > bestDmg) { bestDmg = dmg; best = m; bestMult = mult; }
            }
            double incoming = oppTypes.Max(t => DefMult(x.P, x.Ab, t));
            string speed = oppMon == null ? null : x.P.Stats[5] > oppMon.Stats[5] ? "mais rápido" : x.P.Stats[5] < oppMon.Stats[5] ? "mais lento" : "empata em velocidade";
            double score = (bestDmg * Math.Max(x.P.Stats[1], x.P.Stats[3]) / 100) / Math.Max(0.25, incoming);
            return new Ranked(x, best, bestMult, incoming, speed, score);
        }).OrderByDescending(r => r.Score).ToList();
    }
}

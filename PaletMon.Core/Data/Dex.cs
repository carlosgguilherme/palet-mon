using System.Reflection;
using System.Text.Json;

namespace PaletMon.Data;

public sealed class Evo
{
    public int Level;
    public string Item;
    public string How;
    public string Time;
}

public sealed class Mon
{
    public int Id;
    public int Species;
    public string Name;
    public string[] Types;
    public int[] Stats; // hp, atk, def, spa, spd, spe
    public string[] Abilities; // hidden ones start with '*'
    public int From; // species it evolves from, 0 if none
    public int Chain;
    public int Gen;
    public bool Legendary;
    public bool Mythical;
    public bool Regional;
    public Evo Evo;
    public int[] LevelMoves = Array.Empty<int>(); // pairs: move id, level
    public int[] OtherMoves = Array.Empty<int>(); // TM, tutor, egg

    public int Bst => Stats.Sum();
    public override string ToString() => Name;
}

public sealed class MoveInfo
{
    public int Id;
    public string Name;
    public string Type;
    public int Power; // -1 variable, 0 status
    public int Accuracy; // 101 = never misses
    public int Class; // 1 status, 2 physical, 3 special
    public int Priority;
}

public static class Dex
{
    public static readonly string[] TypeIds = { null, "normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel", "fire", "water", "grass", "electric", "psychic", "ice", "dragon", "dark", "fairy" };

    public static List<Mon> All { get; private set; }
    public static Dictionary<int, Mon> ById { get; private set; }
    public static Dictionary<string, Mon> ByName { get; private set; }
    public static Dictionary<int, MoveInfo> Moves { get; private set; }

    public static void Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("dex.json");
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        Moves = new Dictionary<int, MoveInfo>();
        foreach (var prop in root.GetProperty("moves").EnumerateObject())
        {
            var a = prop.Value;
            var id = int.Parse(prop.Name);
            Moves[id] = new MoveInfo
            {
                Id = id,
                Name = a[0].GetString(),
                Type = TypeIds[a[1].GetInt32()],
                Power = a[2].GetInt32(),
                Accuracy = a[3].GetInt32(),
                Class = a[4].GetInt32(),
                Priority = a[5].GetInt32(),
            };
        }

        All = new List<Mon>();
        foreach (var p in root.GetProperty("pokemon").EnumerateArray())
        {
            var m = new Mon
            {
                Id = p.GetProperty("i").GetInt32(),
                Species = p.GetProperty("s").GetInt32(),
                Name = p.GetProperty("n").GetString(),
                Types = p.GetProperty("t").EnumerateArray().Select(x => TypeIds[x.GetInt32()]).ToArray(),
                Stats = p.GetProperty("b").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                Abilities = p.GetProperty("a").EnumerateArray().Select(x => x.GetString()).ToArray(),
                From = p.GetProperty("f").GetInt32(),
                Chain = p.GetProperty("c").GetInt32(),
                Gen = p.GetProperty("g").GetInt32(),
                Legendary = p.TryGetProperty("L", out _),
                Mythical = p.TryGetProperty("M", out _),
                Regional = p.TryGetProperty("r", out _),
            };
            if (p.TryGetProperty("e", out var e))
            {
                m.Evo = new Evo
                {
                    Level = e.TryGetProperty("lv", out var lv) ? lv.GetInt32() : 0,
                    Item = e.TryGetProperty("item", out var it) ? it.GetString() : null,
                    How = e.TryGetProperty("how", out var how) ? how.GetString() : null,
                    Time = e.TryGetProperty("time", out var tm) ? tm.GetString() : null,
                };
            }
            if (p.TryGetProperty("m", out var mv))
            {
                m.LevelMoves = mv.GetProperty("l").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                m.OtherMoves = mv.GetProperty("o").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            }
            All.Add(m);
        }
        ById = All.ToDictionary(m => m.Id);
        ByName = new Dictionary<string, Mon>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in All) ByName.TryAdd(m.Name, m);
    }

    public static Mon Find(string name) => name != null && ByName.TryGetValue(name.Trim(), out var m) ? m : null;
}

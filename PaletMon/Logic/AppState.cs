using System.Text.Json;
using PaletMon.Data;

namespace PaletMon.Logic;

public sealed class TeamSlot
{
    public int Id { get; set; }
    public int Lv { get; set; }
    public string Ab { get; set; }
}

public sealed class Opponent
{
    public string Mode { get; set; } = "type";
    public string T1 { get; set; } = "water";
    public string T2 { get; set; } = "";
    public string Mon { get; set; } = "";
}

public sealed class BattleMode
{
    public string Preset { get; set; } = "livre";
    public string Format { get; set; } = "singles";
    public int Size { get; set; } = 6;
    public int Pick { get; set; } = 6;
    public string Level { get; set; } = "free";
    public bool BanLegends { get; set; }
    public bool SleepClause { get; set; }

    public BattleMode Clone() => (BattleMode)MemberwiseClone();
}

// Checkbox configuration shared by the Generator and "Montar time" tabs.
public sealed class BoxConfig
{
    public Dictionary<string, bool> Flags { get; set; } = new();
    public string WeatherKind { get; set; } = "rain";
    public string MonoType { get; set; } = "water";
    public int GenNum { get; set; } = 9;

    public bool this[string key]
    {
        get => Flags.TryGetValue(key, out var v) && v;
        set => Flags[key] = value;
    }

    public static BoxConfig With(params string[] on)
    {
        var c = new BoxConfig();
        foreach (var k in on) c[k] = true;
        return c;
    }
}

public sealed class AppState
{
    public List<TeamSlot> Team { get; set; } = new()
    {
        new() { Id = 906, Lv = 11 }, new() { Id = 554, Lv = 25 }, new() { Id = 228, Lv = 29 }, new() { Id = 935, Lv = 25 },
    };
    public List<int> Wish { get; set; } = new();
    public bool Tms { get; set; } = true;
    public bool Finals { get; set; } = true;
    public bool Legends { get; set; }
    public string Tab { get; set; } = "time";
    public Opponent Opp { get; set; } = new();
    public BoxConfig Gen { get; set; } = BoxConfig.With("synergy", "balanced");
    public BoxConfig Build { get; set; } = BoxConfig.With("synergy", "balanced");
    public BattleMode Mode { get; set; } = new();

    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PaletMon", "estado.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppState Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Json);
                if (s != null)
                {
                    s.Team = (s.Team ?? new()).Where(m => Dex.ById.ContainsKey(m.Id)).Take(6).ToList();
                    s.Wish = (s.Wish ?? new()).Where(Dex.ById.ContainsKey).ToList();
                    s.Opp ??= new(); s.Gen ??= BoxConfig.With("synergy", "balanced"); s.Build ??= BoxConfig.With("synergy", "balanced"); s.Mode ??= new();
                    return s;
                }
            }
        }
        catch { }
        return new AppState();
    }

    public static bool ReadOnly;

    public void Save()
    {
        if (ReadOnly) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { }
    }
}

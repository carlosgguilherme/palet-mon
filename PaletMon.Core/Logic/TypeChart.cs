namespace PaletMon.Logic;

public static class TypeChart
{
    public static readonly string[] All = { "normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel", "fire", "water", "grass", "electric", "psychic", "ice", "dragon", "dark", "fairy" };

    public static readonly Dictionary<string, string> Pt = new()
    {
        ["normal"] = "Normal", ["fighting"] = "Lutador", ["flying"] = "Voador", ["poison"] = "Venenoso", ["ground"] = "Terra", ["rock"] = "Pedra",
        ["bug"] = "Inseto", ["ghost"] = "Fantasma", ["steel"] = "Aço", ["fire"] = "Fogo", ["water"] = "Água", ["grass"] = "Planta",
        ["electric"] = "Elétrico", ["psychic"] = "Psíquico", ["ice"] = "Gelo", ["dragon"] = "Dragão", ["dark"] = "Sombrio", ["fairy"] = "Fada",
    };

    public static readonly Dictionary<string, string> Color = new()
    {
        ["normal"] = "#9fa19f", ["fighting"] = "#e07000", ["flying"] = "#6fa8e0", ["poison"] = "#9141cb", ["ground"] = "#915121", ["rock"] = "#a39c70",
        ["bug"] = "#859414", ["ghost"] = "#704170", ["steel"] = "#5590a6", ["fire"] = "#e62829", ["water"] = "#2980ef", ["grass"] = "#3fa129",
        ["electric"] = "#f2b600", ["psychic"] = "#ef4179", ["ice"] = "#3dc4e8", ["dragon"] = "#5060e1", ["dark"] = "#624d4e", ["fairy"] = "#e46ae4",
    };

    public static readonly HashSet<string> DarkText = new() { "electric", "ice", "normal", "rock" };

    static readonly Dictionary<string, (string[] se, string[] nve, string[] im)> Chart = new()
    {
        ["normal"] = (new string[0], new[] { "rock", "steel" }, new[] { "ghost" }),
        ["fighting"] = (new[] { "normal", "rock", "steel", "ice", "dark" }, new[] { "flying", "poison", "bug", "psychic", "fairy" }, new[] { "ghost" }),
        ["flying"] = (new[] { "fighting", "bug", "grass" }, new[] { "rock", "steel", "electric" }, new string[0]),
        ["poison"] = (new[] { "grass", "fairy" }, new[] { "poison", "ground", "rock", "ghost" }, new[] { "steel" }),
        ["ground"] = (new[] { "poison", "rock", "steel", "fire", "electric" }, new[] { "bug", "grass" }, new[] { "flying" }),
        ["rock"] = (new[] { "flying", "bug", "fire", "ice" }, new[] { "fighting", "ground", "steel" }, new string[0]),
        ["bug"] = (new[] { "grass", "psychic", "dark" }, new[] { "fighting", "flying", "poison", "ghost", "steel", "fire", "fairy" }, new string[0]),
        ["ghost"] = (new[] { "ghost", "psychic" }, new[] { "dark" }, new[] { "normal" }),
        ["steel"] = (new[] { "rock", "ice", "fairy" }, new[] { "steel", "fire", "water", "electric" }, new string[0]),
        ["fire"] = (new[] { "bug", "steel", "grass", "ice" }, new[] { "rock", "fire", "water", "dragon" }, new string[0]),
        ["water"] = (new[] { "ground", "rock", "fire" }, new[] { "water", "grass", "dragon" }, new string[0]),
        ["grass"] = (new[] { "ground", "rock", "water" }, new[] { "flying", "poison", "bug", "steel", "fire", "grass", "dragon" }, new string[0]),
        ["electric"] = (new[] { "flying", "water" }, new[] { "grass", "electric", "dragon" }, new[] { "ground" }),
        ["psychic"] = (new[] { "fighting", "poison" }, new[] { "steel", "psychic" }, new[] { "dark" }),
        ["ice"] = (new[] { "flying", "ground", "grass", "dragon" }, new[] { "steel", "fire", "water", "ice" }, new string[0]),
        ["dragon"] = (new[] { "dragon" }, new[] { "steel" }, new[] { "fairy" }),
        ["dark"] = (new[] { "ghost", "psychic" }, new[] { "fighting", "dark", "fairy" }, new string[0]),
        ["fairy"] = (new[] { "fighting", "dragon", "dark" }, new[] { "poison", "steel", "fire" }, new string[0]),
    };

    static readonly double[,] Table = BuildTable();

    static double[,] BuildTable()
    {
        var t = new double[18, 18];
        for (int a = 0; a < 18; a++)
            for (int d = 0; d < 18; d++)
            {
                var c = Chart[All[a]];
                var def = All[d];
                t[a, d] = c.im.Contains(def) ? 0 : c.se.Contains(def) ? 2 : c.nve.Contains(def) ? 0.5 : 1;
            }
        return t;
    }

    public static int Index(string type) => Array.IndexOf(All, type);
    public static double Eff(string atk, string def) => Table[Index(atk), Index(def)];

    public static readonly Dictionary<string, Dictionary<string, double>> AbilityDef = new()
    {
        ["Levitate"] = new() { ["ground"] = 0 }, ["Earth Eater"] = new() { ["ground"] = 0 }, ["Flash Fire"] = new() { ["fire"] = 0 },
        ["Well Baked Body"] = new() { ["fire"] = 0 }, ["Water Absorb"] = new() { ["water"] = 0 }, ["Storm Drain"] = new() { ["water"] = 0 },
        ["Dry Skin"] = new() { ["water"] = 0, ["fire"] = 1.25 }, ["Volt Absorb"] = new() { ["electric"] = 0 }, ["Lightning Rod"] = new() { ["electric"] = 0 },
        ["Motor Drive"] = new() { ["electric"] = 0 }, ["Sap Sipper"] = new() { ["grass"] = 0 }, ["Thick Fat"] = new() { ["fire"] = 0.5, ["ice"] = 0.5 },
        ["Heatproof"] = new() { ["fire"] = 0.5 }, ["Water Bubble"] = new() { ["fire"] = 0.5 }, ["Purifying Salt"] = new() { ["ghost"] = 0.5 },
        ["Fluffy"] = new() { ["fire"] = 2 },
    };

    public static string Mult(double m) => m switch
    {
        0 => "0", 0.25 => "¼", 0.5 => "½", 1 => "1", 1.25 => "1¼", 2 => "2", 4 => "4", _ => m.ToString("0.##"),
    };
}

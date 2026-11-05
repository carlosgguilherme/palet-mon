using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using PaletMon.Data;
using PaletMon.Logic;
using PaletMon.UI;

namespace PaletMon;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // must run first: handles the install, update and uninstall hooks
        Velopack.VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}

public sealed class App : Application
{
    // Palet Mon palette: Amarelo Paleteira, Madeira, Grafite, Concreto.
    static readonly Dictionary<string, (string light, string dark)> Palette = new()
    {
        ["Bg"] = ("#e9e7e2", "#171512"),
        ["Panel"] = ("#f8f7f4", "#211e1a"),
        ["Panel2"] = ("#dedbd4", "#2b2722"),
        ["Line"] = ("#cbc6bc", "#3a352e"),
        ["Fg"] = ("#1f1c19", "#efeae2"),
        ["Muted"] = ("#615a50", "#a39a8d"),
        ["Accent"] = ("#f2b300", "#f5bc1a"),
        ["AccentFg"] = ("#1f1c19", "#1f1c19"),
        ["AccentInk"] = ("#8a6400", "#f5bc1a"),
        ["AccentSoft"] = ("#fbeec4", "#3a3018"),
        ["Wood"] = ("#c4874a", "#d0965a"),
        ["WoodDark"] = ("#8a5a2e", "#9a6a3c"),
        ["Ink"] = ("#1f1c19", "#1f1c19"),
        ["Good"] = ("#1f8a5b", "#4cc78c"),
        ["Warn"] = ("#b7791f", "#e0a84a"),
        ["Bad"] = ("#c2362f", "#ef6b62"),
        ["GoodBg"] = ("#d6efe0", "#183a29"),
        ["WarnBg"] = ("#f6e7c8", "#3d3018"),
        ["BadBg"] = ("#f6d6d3", "#432020"),
        ["ImmBg"] = ("#cfd8e8", "#26304a"),
    };

    public override void Initialize()
    {
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = Color.Parse("#c08e00") };
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = Color.Parse("#f5bc1a") };
        Styles.Add(fluent);
        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();
        foreach (var (key, (l, d)) in Palette)
        {
            light[key] = new SolidColorBrush(Color.Parse(l));
            dark[key] = new SolidColorBrush(Color.Parse(d));
        }
        Resources.ThemeDictionaries[ThemeVariant.Light] = light;
        Resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        RequestedThemeVariant = ThemeVariant.Default;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dex.Load();
        Engine.S = AppState.Load();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}

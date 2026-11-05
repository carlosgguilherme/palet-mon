using Avalonia;
using PaletMon.Logic;

namespace PaletMon;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // must run first: handles the install, update and uninstall hooks
        Velopack.VelopackApp.Build().Run();
        Updater.Current = new DesktopUpdater();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}

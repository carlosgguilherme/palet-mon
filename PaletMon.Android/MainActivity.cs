using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using PaletMon.Logic;

namespace PaletMon.Phone;

[Activity(
    Name = "io.github.carlosgguilherme.paletmon.MainActivity",
    Label = "Palet Mon",
    Theme = "@style/PaletMon.Theme",
    Icon = "@mipmap/icon",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<PaletMon.App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Updater.Current = new AndroidUpdater(global::Android.App.Application.Context);
        return base.CustomizeAppBuilder(builder);
    }
}

using System.Reflection;
using Avalonia.Controls;

namespace PaletMon.UI;

// Desktop host for MainView.
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Palet Mon";
        Width = 1320; Height = 900; MinWidth = 960; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("PaletMon.ico")) Icon = new WindowIcon(icon);
#if SELF_TEST
        // Test builds only: stay out of the way.
        ShowActivated = false; WindowState = WindowState.Minimized;
#endif
        Content = new MainView();
    }
}

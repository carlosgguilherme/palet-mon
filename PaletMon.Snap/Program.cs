using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using PaletMon;
using PaletMon.Logic;
using PaletMon.UI;

if (args.Length == 2 && args[0] == "--golden")
{
    Golden.Write(args[1]);
    return;
}

var dir = args.Length > 0 ? args[0] : "shots";
Directory.CreateDirectory(dir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

AppState.ReadOnly = true;

void Shot(string name, Action prep = null, ThemeVariant theme = null)
{
    Application.Current.RequestedThemeVariant = theme ?? ThemeVariant.Light;
    prep?.Invoke();
    var w = new MainWindow { Width = 1320, Height = 1400 };
    w.Show();
    Dispatcher.UIThread.RunJobs();
    w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    w.Close();
}

Engine.S = new AppState();
Shot("h1-meu-time");
Shot("h2-meu-time-escuro", null, ThemeVariant.Dark);
Shot("h3-batalha-vgc", () => { Engine.ApplyPreset("vgc"); Engine.S.Opp.Mode = "mon"; Engine.S.Opp.Mon = "Gyarados"; Engine.S.Tab = "batalha"; });
Shot("h4-gerador", () => { Engine.ApplyPreset("livre"); Engine.S.Tab = "gerador"; });
Shot("h5-sobre", () => Engine.S.Tab = "sobre");
Console.WriteLine("ok");

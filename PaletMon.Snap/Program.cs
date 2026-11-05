using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using PaletMon;
using PaletMon.Logic;
using PaletMon.UI;

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

void Phone(string name, Action prep = null, ThemeVariant theme = null, Action<MobileView> act = null)
{
    Application.Current.RequestedThemeVariant = theme ?? ThemeVariant.Light;
    prep?.Invoke();
    var view = new MobileView();
    var w = new Window { Width = 412, Height = 880, Content = view };
    w.Show();
    Dispatcher.UIThread.RunJobs();
    act?.Invoke(view);
    for (int i = 0; i < 40; i++) { Thread.Sleep(50); Dispatcher.UIThread.RunJobs(); } // lets images arrive
    w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    w.Close();
}

Engine.S = new AppState();
if (args.Contains("--phone"))
{
    var s = Engine.S;
    Phone("m1-time");
    Phone("m2-time-escuro", null, ThemeVariant.Dark);
    Phone("m3-aberto", null, null, v => v.Expand(1));
    Phone("m4-defesa", null, null, v => v.Section("defesa"));
    Phone("m5-ataque", null, null, v => v.Section("ataque"));
    Phone("m6-montar", () => { s.Tab = "montar"; s.Wish = new() { 445, 282 }; });
    Phone("m7-gerador", () => s.Tab = "gerador", null, v => v.GenerateNow());
    Phone("m8-batalha", () => { s.Tab = "batalha"; Engine.ApplyPreset("vgc"); s.Opp.Mode = "mon"; s.Opp.Mon = "Gyarados"; });
    Phone("m9-sobre", () => { s.Tab = "sobre"; Engine.ApplyPreset("livre"); });
    Phone("m10-modo", () => s.Tab = "time", null, v => v.ShowMode());
    Phone("m11-busca", null, null, v => v.ShowSearch());
    Phone("m12-update", null, ThemeVariant.Dark, v => v.ShowUpdatePrompt());
    Console.WriteLine("ok");
    return;
}
Shot("h1-meu-time");
Shot("h2-meu-time-escuro", null, ThemeVariant.Dark);
Shot("h3-batalha-vgc", () => { Engine.ApplyPreset("vgc"); Engine.S.Opp.Mode = "mon"; Engine.S.Opp.Mon = "Gyarados"; Engine.S.Tab = "batalha"; });
Shot("h4-gerador", () => { Engine.ApplyPreset("livre"); Engine.S.Tab = "gerador"; });
Shot("h5-sobre", () => Engine.S.Tab = "sobre");
Console.WriteLine("ok");

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PaletMon.Logic;
using Velopack;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

// "Verificar atualizações" button, plus the bar under the header that shows what the updater is doing.
public sealed partial class MainWindow
{
    readonly ContentControl updateBar = new() { IsVisible = false };
    Button updateBtn;
    bool updating;

    string updateLabel = "Verificar atualizações";

    static void OpenLink(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    // ---------- Sobre ----------
    Control SobreTab()
    {
        updateBtn = Btn(updateLabel, CheckClicked);
        updateBtn.IsEnabled = !updating;

        var intro = Card(Row(18, Brand.Logo(110).With(l => l.VerticalAlignment = VerticalAlignment.Center), Col(6,
            Brand.Wordmark(30),
            Text("Seu time, bem empilhado.", 16, FontWeight.Bold),
            Muted($"Versão {Updater.Version}", 13))));

        var about = Card(Col(10, H2("O que é o Palet Mon"),
            Text("Um estrategista de time para o Cobbleverse, o modpack de Pokémon para Minecraft. Ele mostra o que seu time aguenta, o que falta e quem levar para cada luta.", 14),
            Col(6,
                Rich("**Meu time:** fraquezas, cobertura de ataque, golpes sugeridos e quando cada Pokémon evolui.", 14),
                Rich("**Montar time:** você escolhe os Pokémon que quer usar e o app completa o resto em volta deles.", 14),
                Rich("**Gerador:** cria um time do zero com as caixas que você marcar, como Aleatório, Sinergia ou Competitivo.", 14),
                Rich("**Batalha:** diz quem mandar contra um tipo ou um Pokémon adversário.", 14),
                Rich("**Modo de combate:** ajusta tudo para Singles, Duplas, Triplas e regras como Nv 50 ou Little Cup.", 14))));

        var updates = Card(Col(10, H2("Atualizações"),
            Text("O app procura novidades sozinho quando abre e avisa no topo da janela. Para procurar agora, use o botão abaixo: se houver uma versão nova, ele baixa, instala e reinicia o app. Seu time e suas opções continuam salvos.", 14),
            Row(10, updateBtn, Muted($"Você está na versão {Updater.Version}.", 13).With(t => t.VerticalAlignment = VerticalAlignment.Center))));

        var credits = Card(Col(10, H2("Créditos"),
            Text("Dados de tipos, atributos, habilidades, evoluções e golpes vêm da PokéAPI. O app usa Avalonia nas telas e Velopack nas atualizações.", 14),
            Text("Palet Mon é um projeto de fã, sem ligação com Nintendo, The Pokémon Company ou Cobblemon.", 14),
            Btn("Abrir no GitHub", () => OpenLink(Updater.RepoUrl), primary: false, small: true)));

        return Col(16, intro, about, updates, credits);
    }

    void ShowBar(string kind, string text, params Control[] actions)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var box = AlertBox(new Alert(kind, text));
        grid.Children.Add(box);
        if (actions.Length > 0)
        {
            var buttons = Row(8, actions);
            buttons.Margin = new Thickness(10, 0, 0, 0);
            buttons.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
        }
        updateBar.Content = grid;
        updateBar.IsVisible = true;
    }

    void HideBar() => updateBar.IsVisible = false;
    Button CloseBarButton(string text = "Fechar") => Btn(text, HideBar, primary: false, small: true);

    void SetBusy(bool busy, string label = "Verificar atualizações")
    {
        updating = busy;
        updateLabel = label;
        if (updateBtn == null) return;
        updateBtn.IsEnabled = !busy;
        updateBtn.Content = label;
    }

    // Quiet check when the app opens: only says something if there is a new version.
    async void AutoCheck()
    {
        if (!Updater.IsInstalled) return;
        try
        {
            var info = await Updater.CheckAsync();
            if (info != null && !updating)
                ShowBar("good", $"Nova versão disponível: **{info.TargetFullRelease.Version}**.",
                    Btn("Atualizar agora", () => _ = Install(info), small: true), CloseBarButton("Depois"));
        }
        catch { }
    }

    async void CheckClicked()
    {
        if (updating) return;
        if (!Updater.IsInstalled)
        {
            ShowBar("neutral", "A atualização automática funciona na versão instalada do Palet Mon. Baixe o instalador para receber as novidades sozinho.",
                Btn("Baixar instalador", () => OpenLink(Updater.ReleasesUrl), small: true), CloseBarButton());
            return;
        }
        SetBusy(true, "Procurando…");
        try
        {
            var info = await Updater.CheckAsync();
            if (info == null)
            {
                SetBusy(false);
                ShowBar("good", $"Você já está na versão mais recente ({Updater.Version}).", CloseBarButton());
                return;
            }
            await Install(info);
        }
        catch
        {
            SetBusy(false);
            ShowBar("warn", "Não consegui procurar atualizações. Confira sua internet e tente de novo.", CloseBarButton());
        }
    }

    async Task Install(UpdateInfo info)
    {
        var version = info.TargetFullRelease.Version;
        SetBusy(true, "Atualizando…");
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Width = 220, VerticalAlignment = VerticalAlignment.Center };
        ShowBar("neutral", $"Baixando a versão **{version}**. O app reinicia sozinho quando terminar.", bar);
        try
        {
            await Updater.DownloadAsync(info, p => Dispatcher.UIThread.Post(() => bar.Value = p));
            S.Save();
            ShowBar("good", $"Instalando a versão **{version}**…");
            Updater.ApplyAndRestart(info);
        }
        catch
        {
            SetBusy(false);
            ShowBar("warn", "A atualização não terminou. Nada mudou no app: confira sua internet e tente de novo.", CloseBarButton());
        }
    }
}

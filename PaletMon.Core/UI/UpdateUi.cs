using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PaletMon.Logic;
using static PaletMon.UI.Ui;

namespace PaletMon.UI;

// "Verificar atualizações" button, plus the bar under the header that shows what the updater is doing.
public sealed partial class MainView
{
    readonly ContentControl updateBar = new() { IsVisible = false };
    readonly UpdateFlow flow = new();
    Button updateBtn;
    ProgressBar progressBar;

    // ---------- Sobre ----------
    Control SobreTab()
    {
        updateBtn = Btn(flow.ButtonLabel, flow.Check);
        updateBtn.IsEnabled = !flow.Busy;

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
            Text("Dados e imagens dos Pokémon vêm da PokéAPI. As imagens são baixadas pela internet e ficam guardadas no aparelho. O app usa Avalonia nas telas e Velopack nas atualizações.", 14),
            Text("Palet Mon é um projeto de fã, sem ligação com Nintendo, The Pokémon Company ou Cobblemon.", 14),
            Btn("Abrir no GitHub", () => OpenLink(this, Updater.RepoUrl), primary: false, small: true)));

        return Col(16, intro, about, updates, credits);
    }

    void WireUpdates()
    {
        flow.OpenLink = url => OpenLink(this, url);
        flow.Changed += ShowNotice;
        flow.StateChanged += () =>
        {
            if (updateBtn == null) return;
            updateBtn.IsEnabled = !flow.Busy;
            updateBtn.Content = flow.ButtonLabel;
        };
        flow.ProgressChanged += p => Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = p; });
    }

    void ShowNotice(UpdateNotice n)
    {
        if (n == null) { updateBar.IsVisible = false; return; }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(AlertBox(new Alert(n.Kind, n.Text)));
        var actions = n.Actions.Select(a => (Control)Btn(a.Label, a.Run, primary: a.Primary, small: true)).ToList();
        progressBar = null;
        if (n.Progress)
        {
            progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Width = 220, VerticalAlignment = VerticalAlignment.Center };
            actions.Add(progressBar);
        }
        if (actions.Count > 0)
        {
            var buttons = Row(8, actions.ToArray());
            buttons.Margin = new Thickness(10, 0, 0, 0);
            buttons.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
        }
        updateBar.Content = grid;
        updateBar.IsVisible = true;
    }
}

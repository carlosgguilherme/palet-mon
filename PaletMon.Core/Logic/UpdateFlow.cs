namespace PaletMon.Logic;

public sealed record UpdateAction(string Label, Action Run, bool Primary = true);
public sealed record UpdateNotice(string Kind, string Text, UpdateAction[] Actions, bool Progress = false);

// What the update button and the check on opening do, independent of how each screen shows it.
public sealed class UpdateFlow
{
    const string IdleLabel = "Verificar atualizações";

    public event Action<UpdateNotice> Changed; // null hides the notice
    public event Action<int> ProgressChanged;
    public event Action StateChanged;

    public bool Busy { get; private set; }
    public string ButtonLabel { get; private set; } = IdleLabel;
    public Action<string> OpenLink;

    static IAppUpdater U => Updater.Current;

    void Show(string kind, string text, params UpdateAction[] actions) => Changed?.Invoke(new UpdateNotice(kind, text, actions));
    public void Hide() => Changed?.Invoke(null);
    UpdateAction Close(string label = "Fechar") => new(label, Hide, false);

    void SetBusy(bool busy, string label = IdleLabel)
    {
        Busy = busy;
        ButtonLabel = label;
        StateChanged?.Invoke();
    }

    static string SizeText(UpdateOffer o) => o.Bytes > 0 ? $" ({Math.Max(1, o.Bytes / (1024 * 1024))} MB)" : "";

    // Quiet check when the app opens: only says something if there is a new version.
    public async void AutoCheck()
    {
        if (!U.IsInstalled) return;
        try
        {
            var offer = await U.CheckAsync();
            if (offer != null && !Busy)
                Show("good", $"Nova versão disponível: **{offer.Version}**. Para atualizar você precisa estar com internet{SizeText(offer)}.",
                    new UpdateAction("Atualizar agora", () => _ = Install(offer)), Close("Agora não"));
        }
        catch { }
    }

    public async void Check()
    {
        if (Busy) return;
        if (!U.IsInstalled)
        {
            Show("neutral", "A atualização automática funciona na versão instalada do Palet Mon. Baixe o instalador para receber as novidades sozinho.",
                new UpdateAction("Baixar instalador", () => OpenLink?.Invoke(Updater.ReleasesUrl)), Close());
            return;
        }
        SetBusy(true, "Procurando…");
        try
        {
            var offer = await U.CheckAsync();
            if (offer == null)
            {
                SetBusy(false);
                Show("good", $"Você já está na versão mais recente ({Updater.Version}).", Close());
                return;
            }
            await Install(offer);
        }
        catch
        {
            SetBusy(false);
            Show("warn", "Não consegui procurar atualizações. Confira sua internet e tente de novo.", Close());
        }
    }

    async Task Install(UpdateOffer offer)
    {
        SetBusy(true, "Atualizando…");
        Changed?.Invoke(new UpdateNotice("neutral", $"Baixando a versão **{offer.Version}**. {U.AfterDownloadNote}", Array.Empty<UpdateAction>(), true));
        try
        {
            await U.DownloadAsync(offer, p => ProgressChanged?.Invoke(p));
            Engine.S.Save();
            Show("good", $"Instalando a versão **{offer.Version}**…");
            Apply(offer);
        }
        catch
        {
            SetBusy(false);
            Show("warn", "A atualização não terminou. Nada mudou no app: confira sua internet e tente de novo.", Close());
        }
    }

    void Apply(UpdateOffer offer)
    {
        var pending = U.ApplyAndRestart(offer);
        SetBusy(false);
        if (pending != null) Show("neutral", pending, new UpdateAction("Instalar", () => Apply(offer)), Close());
    }
}

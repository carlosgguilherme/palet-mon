using System.Reflection;

namespace PaletMon.Logic;

public sealed record UpdateOffer(string Version, object Payload, long Bytes = 0);

// One implementation per platform: Velopack on desktop, GitHub + system installer on Android.
public interface IAppUpdater
{
    bool IsInstalled { get; }
    string AfterDownloadNote { get; }
    Task<UpdateOffer> CheckAsync();
    Task DownloadAsync(UpdateOffer offer, Action<int> progress);

    // Returns a message when the user still has to do something; desktop never returns, it restarts.
    string ApplyAndRestart(UpdateOffer offer);
}

public sealed class NoUpdater : IAppUpdater
{
    public bool IsInstalled => false;
    public string AfterDownloadNote => "";
    public Task<UpdateOffer> CheckAsync() => Task.FromResult<UpdateOffer>(null);
    public Task DownloadAsync(UpdateOffer offer, Action<int> progress) => Task.CompletedTask;
    public string ApplyAndRestart(UpdateOffer offer) => null;
}

public static class Updater
{
    public const string RepoUrl = "https://github.com/carlosgguilherme/palet-mon";
    public const string ReleasesUrl = RepoUrl + "/releases/latest";

    public static IAppUpdater Current = new NoUpdater();

    public static string Version
    {
        get
        {
            var v = typeof(Updater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            return v.Split('+')[0];
        }
    }
}

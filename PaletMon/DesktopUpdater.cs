using PaletMon.Logic;
using Velopack;
using Velopack.Sources;

namespace PaletMon;

// Installs and updates through Velopack, using the GitHub releases of this repository.
public sealed class DesktopUpdater : IAppUpdater
{
    readonly UpdateManager manager = Create();

    static UpdateManager Create()
    {
#if LOCAL_FEED
        return new UpdateManager(Environment.GetEnvironmentVariable("PALETMON_TEST_FEED"));
#else
        return new UpdateManager(new GithubSource(Updater.RepoUrl, null, false));
#endif
    }

    public bool IsInstalled => manager.IsInstalled;
    public string AfterDownloadNote => "O app reinicia sozinho quando terminar.";

    public async Task<UpdateOffer> CheckAsync()
    {
        var info = await manager.CheckForUpdatesAsync();
        return info == null ? null : new UpdateOffer(info.TargetFullRelease.Version.ToString(), info, info.TargetFullRelease.Size);
    }

    public Task DownloadAsync(UpdateOffer offer, Action<int> progress) => manager.DownloadUpdatesAsync((UpdateInfo)offer.Payload, progress);

    public string ApplyAndRestart(UpdateOffer offer)
    {
        manager.ApplyUpdatesAndRestart(((UpdateInfo)offer.Payload).TargetFullRelease);
        return null;
    }
}

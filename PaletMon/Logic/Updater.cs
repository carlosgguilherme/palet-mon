using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace PaletMon.Logic;

// Installs and updates through Velopack, using the GitHub releases of this repository.
public static class Updater
{
    public const string RepoUrl = "https://github.com/carlosgguilherme/palet-mon";
    public const string ReleasesUrl = RepoUrl + "/releases/latest";

    static readonly UpdateManager Manager = Create();

    static UpdateManager Create()
    {
#if LOCAL_FEED
        return new UpdateManager(Environment.GetEnvironmentVariable("PALETMON_TEST_FEED"));
#else
        return new UpdateManager(new GithubSource(RepoUrl, null, false));
#endif
    }

    public static bool IsInstalled => Manager.IsInstalled;

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            return v.Split('+')[0];
        }
    }

    public static Task<UpdateInfo> CheckAsync() => Manager.CheckForUpdatesAsync();
    public static Task DownloadAsync(UpdateInfo info, Action<int> progress) => Manager.DownloadUpdatesAsync(info, progress);
    public static void ApplyAndRestart(UpdateInfo info) => Manager.ApplyUpdatesAndRestart(info.TargetFullRelease);
}

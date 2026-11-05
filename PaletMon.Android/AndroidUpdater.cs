using System.Net.Http.Headers;
using System.Text.Json;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using PaletMon.Logic;

namespace PaletMon.Phone;

// Reads the latest GitHub release, downloads its APK and hands it to the Android installer.
public sealed class AndroidUpdater : IAppUpdater
{
    const string ApiUrl = "https://api.github.com/repos/carlosgguilherme/palet-mon/releases/latest";
    const string ApkName = "Palet-Mon-Android.apk";

    static readonly HttpClient Http = CreateClient();
    readonly Context context;

    public AndroidUpdater(Context context) => this.context = context;

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PaletMon", Updater.Version));
        return http;
    }

    public bool IsInstalled => true;
    public string AfterDownloadNote => "Depois é só confirmar a instalação na tela do Android.";

    string ApkPath => Path.Combine(context.CacheDir.AbsolutePath, "updates", "Palet-Mon.apk");

    public async Task<UpdateOffer> CheckAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(ApiUrl));
        var latest = doc.RootElement.GetProperty("tag_name").GetString().TrimStart('v');
        if (Version.Parse(latest) <= Version.Parse(Updater.Version)) return null;
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            if (asset.GetProperty("name").GetString() == ApkName)
                return new UpdateOffer(latest, asset.GetProperty("browser_download_url").GetString(), asset.GetProperty("size").GetInt64());
        return null;
    }

    public async Task DownloadAsync(UpdateOffer offer, Action<int> progress)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        using var response = await Http.GetAsync((string)offer.Payload, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? offer.Bytes;
        using var input = await response.Content.ReadAsStreamAsync();
        using var output = File.Create(ApkPath);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            done += read;
            if (total > 0) progress((int)(done * 100 / total));
        }
    }

    public string ApplyAndRestart(UpdateOffer offer)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && !context.PackageManager.CanRequestPackageInstalls())
        {
            var settings = new Intent(Settings.ActionManageUnknownAppSources, global::Android.Net.Uri.Parse("package:" + context.PackageName));
            settings.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(settings);
            return "Permita que o Palet Mon instale atualizações, volte aqui e toque em Instalar.";
        }
        var uri = FileProvider.GetUriForFile(context, context.PackageName + ".fileprovider", new Java.IO.File(ApkPath));
        var install = new Intent(Intent.ActionView);
        install.SetDataAndType(uri, "application/vnd.android.package-archive");
        install.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
        context.StartActivity(install);
        return "Confirme a instalação na tela do Android. Se ela não aparecer, toque em Instalar.";
    }
}

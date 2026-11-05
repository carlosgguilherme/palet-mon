using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PaletMon.Data;

namespace PaletMon.UI;

// Pokémon images are not shipped with the app: they come from PokéAPI on demand and stay cached on disk.
public static class Sprites
{
    const string Base = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaletMon", "imagens");
    static readonly Dictionary<string, Task<Bitmap>> Cache = new();

    static Task<Bitmap> Get(int id, bool art)
    {
        var key = (art ? "a" : "p") + id;
        if (!Cache.TryGetValue(key, out var task)) Cache[key] = task = Fetch(id, art, key);
        return task;
    }

    static async Task<Bitmap> Fetch(int id, bool art, string key)
    {
        try
        {
            var file = Path.Combine(Folder, key + ".png");
            byte[] bytes;
            if (File.Exists(file)) bytes = await File.ReadAllBytesAsync(file);
            else
            {
                bytes = await Http.GetByteArrayAsync(art ? $"{Base}/other/official-artwork/{id}.png" : $"{Base}/{id}.png");
                Directory.CreateDirectory(Folder);
                await File.WriteAllBytesAsync(file, bytes);
            }
            using var stream = new MemoryStream(bytes);
            return art ? Bitmap.DecodeToWidth(stream, 256) : new Bitmap(stream);
        }
        catch
        {
            Cache.Remove(key); // no internet: try again next time
            return null;
        }
    }

    // art: large official artwork. Otherwise the small pixel sprite, light enough for long lists.
    public static Control View(Mon p, double size, bool art = true, Control fallback = null)
    {
        var grid = new Grid { Width = size, Height = size };
        if (fallback != null) grid.Children.Add(fallback);
        var image = new Image { Stretch = Stretch.Uniform };
        if (!art) RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        grid.Children.Add(image);
        Load();
        return grid;

        async void Load()
        {
            var bitmap = await Get(p.Id, art);
            if (bitmap == null) return;
            image.Source = bitmap;
            if (fallback != null) fallback.IsVisible = false;
        }
    }
}

using GameLauncher.Helpers;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Imagens escolhidas à mão. São copiadas para %LocalAppData%\GameLauncher\art, para continuarem a
/// funcionar mesmo que o ficheiro original seja apagado ou mudado de sítio.
/// "Limpar cache" não lhes toca.
/// </summary>
public static class CustomArtStore
{
    public static string ArtDir => Path.Combine(DataStore.Root, "art");

    /// <summary>Formatos aceites no seletor de ficheiros.</summary>
    public const string FileFilterPattern = "*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif";

    public static string? Get(CustomArtwork? art, ArtKind kind)
    {
        var path = art is null ? null : kind switch
        {
            ArtKind.Cover => art.Cover,
            ArtKind.Hero => art.Hero,
            ArtKind.Logo => art.Logo,
            _ => art.Header,
        };
        return path is not null && File.Exists(path) ? path : null;
    }

    public static void Set(CustomArtwork art, ArtKind kind, string? path)
    {
        switch (kind)
        {
            case ArtKind.Cover: art.Cover = path; break;
            case ArtKind.Hero: art.Hero = path; break;
            case ArtKind.Logo: art.Logo = path; break;
            default: art.Header = path; break;
        }
    }

    /// <summary>Confirma que o ficheiro é uma imagem que a app consegue abrir.</summary>
    public static bool IsReadableImage(string path) => ImageHelper.Load(path, 32) is not null;

    /// <summary>Copia a imagem para a pasta da app e devolve o caminho da cópia.</summary>
    public static string Import(string source, string gameId, ArtKind kind)
    {
        Directory.CreateDirectory(ArtDir);
        var safe = string.Concat(gameId.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));
        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext.Length == 0) ext = ".img";
        // Nome sempre novo: assim a imagem antiga não fica presa na memória com o mesmo caminho.
        var target = Path.Combine(ArtDir, $"{safe}_{kind.ToString().ToLowerInvariant()}_{DateTime.UtcNow.Ticks}{ext}");
        File.Copy(source, target, overwrite: true);
        return target;
    }

    public static void Delete(string? path)
    {
        if (path is null) return;
        try
        {
            // Só apaga ficheiros da própria pasta da app.
            if (Path.GetFullPath(path).StartsWith(Path.GetFullPath(ArtDir), StringComparison.OrdinalIgnoreCase))
                File.Delete(path);
        }
        catch { }
    }
}

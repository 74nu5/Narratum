using System.Text.RegularExpressions;

namespace Narratum.Web.Services;

/// <summary>
/// Enregistre les images générées sur disque (hors wwwroot) et renvoie l'URL relative sous laquelle
/// elles sont servies (voir la configuration <c>UseStaticFiles</c> dans Program.cs).
/// </summary>
public sealed partial class ImageStorageService
{
    /// <summary>Segment d'URL sous lequel les images sont exposées.</summary>
    public const string RequestPath = "/generated-images";

    private readonly string _root;

    public ImageStorageService(IWebHostEnvironment environment)
    {
        this._root = Path.Combine(environment.ContentRootPath, "generated-images");
        Directory.CreateDirectory(this._root);
    }

    /// <summary>Dossier physique où les images sont stockées (servi sous <see cref="RequestPath"/>).</summary>
    public string RootPath => this._root;

    /// <summary>
    /// Écrit les octets d'image pour une page et renvoie l'URL relative pour l'afficher.
    /// </summary>
    public async Task<string> SaveAsync(
        string slotName, int pageIndex, byte[] bytes, string extension, CancellationToken ct = default)
    {
        var safeSlot = Sanitize(slotName);
        var directory = Path.Combine(this._root, safeSlot);
        Directory.CreateDirectory(directory);

        var fileName = $"{pageIndex}.{extension}";
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), bytes, ct);

        return $"{RequestPath}/{safeSlot}/{fileName}";
    }

    private static string Sanitize(string value)
        => SafeName().Replace(value, "_");

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex SafeName();
}

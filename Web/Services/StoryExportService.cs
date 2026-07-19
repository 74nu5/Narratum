namespace Narratum.Web.Services;

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using Narratum.Core;

/// <summary>
/// Compiles a whole story into a portable document — Markdown for a quick read, or a real
/// EPUB (illustrations embedded) so the author leaves with a book rather than a database.
/// </summary>
public sealed partial class StoryExportService
{
    private readonly IStoryRepository _repository;
    private readonly ImageStorageService _imageStorage;

    public StoryExportService(IStoryRepository repository, ImageStorageService imageStorage)
    {
        this._repository = repository;
        this._imageStorage = imageStorage;
    }

    /// <summary>A page as the exporters need it: prose plus an optional illustration.</summary>
    private sealed record ExportPage(int Index, string Narrative, string? ImagePath, string? ModelUsed);

    /// <summary>Title, genre and the pages worth reading, shared by every export format.</summary>
    private async Task<(string Title, string? Genre, List<ExportPage> Pages)> LoadAsync(
        string slotName, CancellationToken ct)
    {
        var title = await this._repository.GetDisplayNameAsync(slotName, ct);
        if (string.IsNullOrWhiteSpace(title))
            title = slotName;

        var stories = await this._repository.ListStoriesAsync(ct);
        var genre = stories.FirstOrDefault(s => s.SlotName == slotName)?.GenreStyle;

        var indices = await this._repository.GetPageHistoryAsync(slotName, ct);

        // Page 0 is the seed snapshot (a recap of world and cast), not prose — leave it out
        // of the reading export as soon as the story has actual written pages.
        var readable = indices.Where(i => i > 0).ToList();
        if (readable.Count == 0)
            readable = [.. indices];

        var pages = new List<ExportPage>();
        foreach (var index in readable)
        {
            var result = await this._repository.LoadPageAsync(slotName, index, ct);
            if (result is not Result<PageSnapshot>.Success success)
                continue;

            var imagePath = await this._repository.GetPageImageAsync(slotName, index, ct);
            pages.Add(new ExportPage(
                index,
                (success.Value.NarrativeText ?? string.Empty).Trim(),
                imagePath,
                success.Value.ModelUsed));
        }

        return (title, genre, pages);
    }

    // ---- Markdown ---------------------------------------------------------------------

    /// <summary>Builds the Markdown document. Returns the suggested file name and the content.</summary>
    public async Task<(string FileName, string Markdown)> ToMarkdownAsync(
        string slotName, CancellationToken ct = default)
    {
        var (title, genre, pages) = await this.LoadAsync(slotName, ct);

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(title).AppendLine();

        if (!string.IsNullOrWhiteSpace(genre))
            sb.Append('*').Append(genre).AppendLine("*").AppendLine();

        sb.Append("> Exporté depuis Narratum le ")
          .Append(DateTime.Now.ToString("dd/MM/yyyy à HH:mm"))
          .Append(" · ").Append(pages.Count).Append(pages.Count > 1 ? " pages" : " page")
          .AppendLine().AppendLine();

        foreach (var page in pages)
        {
            sb.AppendLine("---").AppendLine();
            sb.Append("## Page ").Append(page.Index).AppendLine().AppendLine();

            if (!string.IsNullOrWhiteSpace(page.ImagePath))
                sb.Append("![Illustration de la page ").Append(page.Index).Append("](")
                  .Append(page.ImagePath).AppendLine(")").AppendLine();

            if (page.Narrative.Length > 0)
                sb.AppendLine(NormalizeParagraphs(page.Narrative)).AppendLine();

            if (!string.IsNullOrWhiteSpace(page.ModelUsed) && page.ModelUsed != "N/A")
                sb.Append("<sub>Généré avec ").Append(ModelDisplay.Badge(page.ModelUsed, []))
                  .AppendLine("</sub>").AppendLine();
        }

        return ($"{Sanitize(title)}.md", sb.ToString());
    }

    // ---- EPUB -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal but valid EPUB 3 (hand-rolled — no third-party dependency), with each
    /// page as a chapter and the illustrations embedded in the archive.
    /// </summary>
    public async Task<(string FileName, byte[] Content)> ToEpubAsync(
        string slotName, CancellationToken ct = default)
    {
        var (title, genre, pages) = await this.LoadAsync(slotName, ct);

        // Collect the illustrations that actually exist on disk, keyed by page.
        var images = new Dictionary<int, (string EntryName, string MediaType, byte[] Bytes)>();
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.ImagePath))
                continue;

            var physical = this.ResolveImagePath(page.ImagePath);
            if (physical is null || !File.Exists(physical))
                continue;

            var extension = Path.GetExtension(physical).ToLowerInvariant();
            images[page.Index] = (
                $"images/page-{page.Index}{extension}",
                MediaTypeFor(extension),
                await File.ReadAllBytesAsync(physical, ct));
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            // The mimetype entry must come first and be stored uncompressed.
            var mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            await using (var writer = new StreamWriter(mimetype.Open(), new UTF8Encoding(false)))
                await writer.WriteAsync("application/epub+zip");

            await WriteEntryAsync(zip, "META-INF/container.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """, ct);

            await WriteEntryAsync(zip, "OEBPS/style.css",
                """
                body { font-family: Georgia, serif; line-height: 1.7; margin: 1.2em; }
                h1 { font-size: 1.6em; font-weight: 500; }
                h2 { font-size: 1.2em; font-weight: 500; color: #b5651d; }
                p { margin: 0 0 1em; text-align: justify; }
                img { max-width: 100%; height: auto; display: block; margin: 0 auto 1.2em; }
                .meta { color: #6a6154; font-size: .85em; }
                """, ct);

            // Chapters.
            foreach (var page in pages)
            {
                var body = new StringBuilder();
                if (images.TryGetValue(page.Index, out var image))
                    body.Append("<img src=\"").Append(image.EntryName)
                        .Append("\" alt=\"Illustration de la page ").Append(page.Index).Append("\"/>\n");

                foreach (var paragraph in SplitParagraphs(page.Narrative))
                    body.Append("<p>").Append(WebUtility.HtmlEncode(paragraph)).Append("</p>\n");

                await WriteEntryAsync(zip, $"OEBPS/page-{page.Index}.xhtml",
                    $"""
                     <?xml version="1.0" encoding="UTF-8"?>
                     <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="fr" lang="fr">
                     <head><title>Page {page.Index}</title><link rel="stylesheet" type="text/css" href="style.css"/></head>
                     <body>
                     <h2>Page {page.Index}</h2>
                     {body}
                     </body>
                     </html>
                     """, ct);
            }

            // Navigation document.
            var nav = new StringBuilder();
            foreach (var page in pages)
                nav.Append("<li><a href=\"page-").Append(page.Index).Append(".xhtml\">Page ")
                   .Append(page.Index).Append("</a></li>\n");

            await WriteEntryAsync(zip, "OEBPS/nav.xhtml",
                $"""
                 <?xml version="1.0" encoding="UTF-8"?>
                 <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" xml:lang="fr" lang="fr">
                 <head><title>Sommaire</title></head>
                 <body>
                 <nav epub:type="toc" id="toc"><h1>Sommaire</h1><ol>
                 {nav}
                 </ol></nav>
                 </body>
                 </html>
                 """, ct);

            // Package document: manifest + spine.
            var manifest = new StringBuilder();
            var spine = new StringBuilder();

            manifest.AppendLine("""    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>""");
            manifest.AppendLine("""    <item id="css" href="style.css" media-type="text/css"/>""");

            foreach (var page in pages)
            {
                manifest.Append("    <item id=\"page").Append(page.Index)
                        .Append("\" href=\"page-").Append(page.Index)
                        .AppendLine(".xhtml\" media-type=\"application/xhtml+xml\"/>");
                spine.Append("    <itemref idref=\"page").Append(page.Index).AppendLine("\"/>");
            }

            foreach (var (pageIndex, image) in images)
                manifest.Append("    <item id=\"img").Append(pageIndex)
                        .Append("\" href=\"").Append(image.EntryName)
                        .Append("\" media-type=\"").Append(image.MediaType).AppendLine("\"/>");

            var subject = string.IsNullOrWhiteSpace(genre)
                ? string.Empty
                : $"\n    <dc:subject>{WebUtility.HtmlEncode(genre)}</dc:subject>";

            await WriteEntryAsync(zip, "OEBPS/content.opf",
                $"""
                 <?xml version="1.0" encoding="UTF-8"?>
                 <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">
                   <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                     <dc:identifier id="bookid">urn:narratum:{WebUtility.HtmlEncode(slotName)}</dc:identifier>
                     <dc:title>{WebUtility.HtmlEncode(title)}</dc:title>
                     <dc:language>fr</dc:language>
                     <dc:creator>Narratum</dc:creator>{subject}
                     <meta property="dcterms:modified">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>
                   </metadata>
                   <manifest>
                 {manifest}  </manifest>
                   <spine>
                 {spine}  </spine>
                 </package>
                 """, ct);

            // Illustration bytes.
            foreach (var (_, image) in images)
            {
                var entry = zip.CreateEntry($"OEBPS/{image.EntryName}", CompressionLevel.Fastest);
                await using var stream = entry.Open();
                await stream.WriteAsync(image.Bytes, ct);
            }
        }

        return ($"{Sanitize(title)}.epub", buffer.ToArray());
    }

    /// <summary>Maps a served image URL (/generated-images/slot/1.png) back to a file on disk.</summary>
    private string? ResolveImagePath(string servedPath)
    {
        const string prefix = ImageStorageService.RequestPath + "/";
        if (!servedPath.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var relative = servedPath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(this._imageStorage.RootPath, relative);
    }

    private static string MediaTypeFor(string extension) => extension switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream",
    };

    private static async Task WriteEntryAsync(
        ZipArchive zip, string name, string content, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), ct);
    }

    private static IEnumerable<string> SplitParagraphs(string text)
        => text.Replace("\r\n", "\n")
               .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Where(p => p.Length > 0);

    /// <summary>Collapses runs of blank lines so the Markdown keeps clean paragraph breaks.</summary>
    private static string NormalizeParagraphs(string text)
        => BlankLines().Replace(text.Replace("\r\n", "\n"), "\n\n");

    private static string Sanitize(string value)
        => UnsafeFileChars().Replace(value, "_").Trim('_', ' ');

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"[^\p{L}\p{N} _.-]")]
    private static partial Regex UnsafeFileChars();
}

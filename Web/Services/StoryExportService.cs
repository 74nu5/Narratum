namespace Narratum.Web.Services;

using System.Text;
using System.Text.RegularExpressions;

using Narratum.Core;

/// <summary>
/// Compiles a whole story into a single Markdown document — prose, illustrations and the
/// per-page provenance — so the author can leave with a readable file rather than a database.
/// </summary>
public sealed partial class StoryExportService
{
    private readonly IStoryRepository _repository;

    public StoryExportService(IStoryRepository repository) => this._repository = repository;

    /// <summary>
    /// Builds the Markdown document for a story. Returns the suggested file name and the content.
    /// Pages that failed to load are skipped rather than aborting the whole export.
    /// </summary>
    public async Task<(string FileName, string Markdown)> ToMarkdownAsync(
        string slotName, CancellationToken ct = default)
    {
        var title = await this._repository.GetDisplayNameAsync(slotName, ct);
        var pageIndices = await this._repository.GetPageHistoryAsync(slotName, ct);

        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(string.IsNullOrWhiteSpace(title) ? slotName : title);
        sb.AppendLine();

        // The genre lives on the library entry, not on the page snapshots.
        var stories = await this._repository.ListStoriesAsync(ct);
        var genre = stories.FirstOrDefault(s => s.SlotName == slotName)?.GenreStyle;
        if (!string.IsNullOrWhiteSpace(genre))
            sb.Append('*').Append(genre).AppendLine("*").AppendLine();

        // Page 0 is the seed snapshot (a recap of world and cast), not prose — leave it out
        // of the reading export as soon as the story has actual written pages.
        var narrativePages = pageIndices.Where(i => i > 0).ToList();
        if (narrativePages.Count == 0)
            narrativePages = [.. pageIndices];

        sb.Append("> Exporté depuis Narratum le ")
          .Append(DateTime.Now.ToString("dd/MM/yyyy à HH:mm"))
          .Append(" · ").Append(narrativePages.Count).Append(narrativePages.Count > 1 ? " pages" : " page")
          .AppendLine();
        sb.AppendLine();

        foreach (var pageIndex in narrativePages)
        {
            var pageResult = await this._repository.LoadPageAsync(slotName, pageIndex, ct);
            if (pageResult is not Result<PageSnapshot>.Success success)
                continue;

            var page = success.Value;

            sb.AppendLine("---").AppendLine();
            sb.Append("## Page ").Append(pageIndex).AppendLine().AppendLine();

            var imagePath = await this._repository.GetPageImageAsync(slotName, pageIndex, ct);
            if (!string.IsNullOrWhiteSpace(imagePath))
                sb.Append("![Illustration de la page ").Append(pageIndex).Append("](")
                  .Append(imagePath).AppendLine(")").AppendLine();

            var narrative = (page.NarrativeText ?? string.Empty).Trim();
            if (narrative.Length > 0)
                sb.AppendLine(NormalizeParagraphs(narrative)).AppendLine();

            if (!string.IsNullOrWhiteSpace(page.ModelUsed) && page.ModelUsed != "N/A")
                sb.Append("<sub>Généré avec ").Append(ModelDisplay.Badge(page.ModelUsed, []))
                  .AppendLine("</sub>").AppendLine();
        }

        return ($"{Sanitize(string.IsNullOrWhiteSpace(title) ? slotName : title)}.md", sb.ToString());
    }

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

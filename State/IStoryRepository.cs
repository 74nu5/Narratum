using Narratum.State;

namespace Narratum.Core;

/// <summary>
/// Repository abstraction for story persistence operations.
/// Isolates Web/UI layer from direct database access to maintain hexagonal architecture.
/// </summary>
public interface IStoryRepository
{
    /// <summary>
    /// Creates a new story slot with initial metadata and page 0.
    /// </summary>
    Task<Result<StoryMetadata>> CreateStoryAsync(
        string slotName,
        string worldName,
        string genreStyle,
        string displayDescription,
        StoryState initialState,
        string initialNarrativeText,
        string initialModel,
        CancellationToken ct = default);

    /// <summary>
    /// Saves a new page snapshot for an existing story.
    /// </summary>
    Task<Result<PageSnapshot>> SavePageAsync(
        string slotName,
        int pageIndex,
        string narrativeText,
        string intentDescription,
        string modelUsed,
        StoryState currentState,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a specific page snapshot.
    /// </summary>
    Task<Result<PageSnapshot>> LoadPageAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Loads the latest page snapshot for a story.
    /// </summary>
    Task<Result<PageSnapshot>> LoadLatestPageAsync(
        string slotName,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all stories in the library.
    /// </summary>
    Task<List<StoryEntry>> ListStoriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes all pages and metadata for a story slot.
    /// </summary>
    Task DeleteStoryAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Checks if a story slot exists.
    /// </summary>
    Task<bool> StoryExistsAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Deletes every page strictly after <paramref name="pageIndex"/>. Used when the author
    /// rewrites the story from an earlier page: the pages that followed no longer belong to it.
    /// Returns how many pages were removed.
    /// </summary>
    Task<int> TruncatePagesAfterAsync(string slotName, int pageIndex, CancellationToken ct = default);

    /// <summary>
    /// Gets all page indices for a story (timeline).
    /// </summary>
    Task<List<int>> GetPageHistoryAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Maps each page index to the model id it was generated with (for provider indicators).
    /// </summary>
    Task<IReadOnlyDictionary<int, string?>> GetPageModelsAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Stores Expert-mode data (e.g. serialized per-agent traces) for a saved page.
    /// </summary>
    Task SavePageExpertDataAsync(string slotName, int pageIndex, string expertData, CancellationToken ct = default);

    /// <summary>
    /// Reads the Expert-mode data for a page, or null if none was stored.
    /// </summary>
    Task<string?> GetPageExpertDataAsync(string slotName, int pageIndex, CancellationToken ct = default);

    /// <summary>
    /// Stores the serialized next-step choices proposed for a saved page.
    /// </summary>
    Task SavePageChoicesAsync(string slotName, int pageIndex, string choicesJson, CancellationToken ct = default);

    /// <summary>
    /// Reads the serialized next-step choices for a page, or null if none were stored.
    /// </summary>
    Task<string?> GetPageChoicesAsync(string slotName, int pageIndex, CancellationToken ct = default);

    /// <summary>
    /// Stores the serialized character roster for a saved page.
    /// </summary>
    Task SavePageCharactersAsync(string slotName, int pageIndex, string charactersJson, CancellationToken ct = default);

    /// <summary>
    /// Reads the serialized character roster for a page, or null if none was stored.
    /// </summary>
    Task<string?> GetPageCharactersAsync(string slotName, int pageIndex, CancellationToken ct = default);

    /// <summary>
    /// Stores the serialized secrets produced for a saved page.
    /// </summary>
    Task SavePageSecretsAsync(string slotName, int pageIndex, string secretsJson, CancellationToken ct = default);

    /// <summary>
    /// Reads the serialized secrets for a page, or null if none were stored.
    /// </summary>
    Task<string?> GetPageSecretsAsync(string slotName, int pageIndex, CancellationToken ct = default);

    /// <summary>
    /// Returns the serialized secrets JSON of every page (in order) — used to accumulate hidden
    /// secrets for narrative continuity.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllPageSecretsAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Stores the generated image path and its prompt for a saved page.
    /// </summary>
    Task SavePageImageAsync(string slotName, int pageIndex, string imagePath, string imagePrompt, CancellationToken ct = default);

    /// <summary>
    /// Reads the generated image path for a page, or null if none.
    /// </summary>
    Task<string?> GetPageImageAsync(string slotName, int pageIndex, CancellationToken ct = default);

    /// <summary>
    /// Returns the concatenated narrative text of every page (in order) — the story so far.
    /// </summary>
    Task<string> GetStoryTextAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Gets the display name for a story slot.
    /// </summary>
    Task<string> GetDisplayNameAsync(string slotName, CancellationToken ct = default);
}

/// <summary>
/// Story metadata returned by repository operations.
/// </summary>
public record StoryMetadata(
    string SlotName,
    string WorldName,
    string GenreStyle,
    DateTime CreatedAt,
    int TotalPages);

/// <summary>
/// Page snapshot data returned by repository.
/// </summary>
public record PageSnapshot(
    string SlotName,
    int PageIndex,
    string NarrativeText,
    string IntentDescription,
    string ModelUsed,
    DateTime GeneratedAt,
    StoryState State);

/// <summary>
/// Story entry for library listing.
/// </summary>
public record StoryEntry(
    string SlotName,
    string DisplayName,
    string Description,
    int PageCount,
    DateTime LastUpdated,
    string GenreStyle);

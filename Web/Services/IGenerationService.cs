using Narratum.Core;
using Narratum.Orchestration.Models;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Interface for narrative generation service.
/// Allows mocking in tests and decouples Blazor components from implementation.
/// </summary>
public interface IGenerationService
{
    /// <summary>
    /// Creates a new story slot with initial state.
    /// </summary>
    /// <param name="slotName">Unique identifier for the story slot</param>
    /// <param name="request">Story creation parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success with slot name, or failure with error message</returns>
    Task<Result<string>> CreateStoryAsync(
        string slotName,
        StoryCreationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Generates the next page of narrative content.
    /// </summary>
    /// <param name="slotName">Story slot identifier</param>
    /// <param name="intentDescription">User's narrative direction</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success with page info, or failure with error message</returns>
    Task<Result<PageInfo>> GenerateNextPageAsync(
        string slotName,
        string intentDescription,
        CancellationToken ct = default);

    /// <summary>
    /// Generates the next page, streaming the narrative text fragment-by-fragment as the
    /// model produces it. The concatenation of all yielded fragments is the full page text;
    /// the page is persisted once streaming completes. Throws on validation, generation or
    /// save errors (surfaced to the caller's try/catch).
    /// </summary>
    /// <param name="slotName">Story slot identifier</param>
    /// <param name="intentDescription">User's narrative direction</param>
    /// <param name="model">LLM model to use for this page; null falls back to the default.</param>
    /// <param name="imageModel">Image model for this page; null/none skips image generation.</param>
    /// <param name="ct">Cancellation token</param>
    IAsyncEnumerable<string> GenerateNextPageStreamingAsync(
        string slotName,
        string intentDescription,
        string? model = null,
        string? imageModel = null,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a specific page from the story.
    /// </summary>
    /// <param name="slotName">Story slot identifier</param>
    /// <param name="pageIndex">Zero-based page index</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success with page info, or failure with error message</returns>
    Task<Result<PageInfo>> LoadPageAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all page indices for a story (timeline).
    /// </summary>
    /// <param name="slotName">Story slot identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of page indices</returns>
    Task<List<int>> GetPageHistoryAsync(
        string slotName,
        CancellationToken ct = default);

    /// <summary>
    /// Maps each page index to the model id it was generated with (for provider indicators).
    /// </summary>
    Task<IReadOnlyDictionary<int, string?>> GetPageModelsAsync(
        string slotName,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the display name for a story slot.
    /// </summary>
    /// <param name="slotName">Story slot identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Display name (falls back to slot name)</returns>
    Task<string> GetDisplayNameAsync(
        string slotName,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the per-agent traces recorded for a page (Expert mode). Empty when the page
    /// was generated without multi-agent data (e.g. older pages).
    /// </summary>
    Task<IReadOnlyList<AgentTraceInfo>> GetAgentTraceAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the next-step choices proposed for a page. Empty when none were stored
    /// (e.g. older pages, or the page is still generating).
    /// </summary>
    Task<IReadOnlyList<StoryChoice>> GetPageChoicesAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the character roster recorded for a page. Empty when none was stored.
    /// </summary>
    Task<IReadOnlyList<CharacterProfile>> GetPageCharactersAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the secrets recorded for a page (revealed and hidden). Empty when none were stored.
    /// </summary>
    Task<IReadOnlyList<StorySecret>> GetPageSecretsAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the generated image URL for a page, or null if none.
    /// </summary>
    Task<string?> GetPageImageAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Re-runs the illustration for an existing page — the image agent can fail (or disappoint)
    /// while the page itself is fine, and the author shouldn't have to regenerate the prose.
    /// Pass <paramref name="imagePromptOverride"/> to render the author's own wording; otherwise
    /// a fresh visual prompt is derived from the page text. Returns the served image URL.
    /// </summary>
    Task<Result<string>> RegeneratePageImageAsync(
        string slotName,
        int pageIndex,
        string imageModel,
        string? imagePromptOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// The visual prompt to show the author before regenerating: the one that produced the
    /// current illustration, or a freshly derived one when none was ever recorded.
    /// </summary>
    Task<string> SuggestImagePromptAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the pages after <paramref name="pageIndex"/> so the story can be rewritten from
    /// there. Returns how many pages were removed.
    /// </summary>
    Task<int> TruncateAfterAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Re-runs the choice agent for a page, when the three proposed continuations don't inspire.
    /// </summary>
    Task<Result<IReadOnlyList<StoryChoice>>> RegeneratePageChoicesAsync(
        string slotName,
        int pageIndex,
        string? model = null,
        CancellationToken ct = default);

    /// <summary>Replaces a page's prose with the author's own text.</summary>
    Task UpdatePageTextAsync(
        string slotName,
        int pageIndex,
        string narrativeText,
        CancellationToken ct = default);

    /// <summary>Renames a story (its library display name).</summary>
    Task RenameStoryAsync(
        string slotName,
        string displayName,
        CancellationToken ct = default);

    /// <summary>
    /// Starts a fresh run of the same universe: the world bible and the seed page are cloned
    /// into a new slot, leaving the pages behind. The source's opening action is returned so the
    /// caller can replay it — same beginning, then the choices make the two runs drift apart.
    /// </summary>
    Task<Result<StoryRun>> DuplicateStoryAsync(
        string sourceSlotName,
        CancellationToken ct = default);

    /// <summary>
    /// Starts a new run of a universe: a story seeded from the setting, attached to it, named
    /// « <em>univers</em> — partie N ». The universe's opening action is returned to be replayed.
    /// </summary>
    Task<Result<StoryRun>> StartRunAsync(
        string universeId,
        CancellationToken ct = default);

    /// <summary>
    /// The setting this run started from — the snapshot frozen at its creation, which is also what
    /// its prompts are built on. Null when the story predates the world bible.
    /// </summary>
    Task<StoryWorld?> GetRunWorldAsync(
        string slotName,
        CancellationToken ct = default);
}

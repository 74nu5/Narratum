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
    /// <param name="ct">Cancellation token</param>
    IAsyncEnumerable<string> GenerateNextPageStreamingAsync(
        string slotName,
        string intentDescription,
        string? model = null,
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
}

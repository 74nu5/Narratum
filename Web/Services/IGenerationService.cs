using Narratum.Core;
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
    /// Gets the display name for a story slot.
    /// </summary>
    /// <param name="slotName">Story slot identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Display name (falls back to slot name)</returns>
    Task<string> GetDisplayNameAsync(
        string slotName,
        CancellationToken ct = default);
}

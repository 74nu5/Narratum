using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Simulation;

/// <summary>
/// Service orchestrating the progression of the narrative story.
/// Coordinates transitions between chapters and manages overall story flow.
/// </summary>
public interface IProgressionService
{
    /// <summary>
    /// Progresses the story by applying an action and returning the new state.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <param name="action">The action to apply.</param>
    /// <returns>New state after progression.</returns>
    Result<StoryState> Progress(StoryState state, StoryAction action);

    /// <summary>
    /// Gets the current chapter from the state.
    /// </summary>
    StoryChapter? GetCurrentChapter(StoryState state);

    /// <summary>
    /// Determines if the current chapter can be advanced.
    /// </summary>
    bool CanAdvanceChapter(StoryState state);

    /// <summary>
    /// Advances to the next chapter.
    /// </summary>
    Result<StoryState> AdvanceChapter(StoryState state);

    /// <summary>
    /// Gets the narrative history (all events in order).
    /// </summary>
    IReadOnlyList<Domain.Event> GetEventHistory(StoryState state);

    /// <summary>
    /// Gets the count of events in the current story.
    /// </summary>
    int GetEventCount(StoryState state);
}

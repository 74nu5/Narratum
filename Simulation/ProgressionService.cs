using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Simulation;

/// <summary>
/// Implementation of progression service.
/// Orchestrates story progression and chapter management.
/// </summary>
public class ProgressionService : IProgressionService
{
    private readonly IStateTransitionService _transitionService;

    public ProgressionService(IStateTransitionService transitionService)
    {
        _transitionService = transitionService ?? throw new ArgumentNullException(nameof(transitionService));
    }

    public Result<StoryState> Progress(StoryState state, StoryAction action)
    {
        if (state == null)
            return Result<StoryState>.Fail("State cannot be null");
        if (action == null)
            return Result<StoryState>.Fail("Action cannot be null");

        // Use transition service to apply the action
        return _transitionService.TransitionState(state, action);
    }

    public StoryChapter? GetCurrentChapter(StoryState state)
    {
        if (state == null)
            return null;
        
        return state.CurrentChapter;
    }

    public bool CanAdvanceChapter(StoryState state)
    {
        if (state == null)
            return false;
        
        var currentChapter = state.CurrentChapter;
        if (currentChapter == null)
            return false;
        
        // Can advance if chapter is in progress
        return currentChapter.Status == StoryProgressStatus.InProgress;
    }

    public Result<StoryState> AdvanceChapter(StoryState state)
    {
        if (state == null)
            return Result<StoryState>.Fail("State cannot be null");
        
        var currentChapter = state.CurrentChapter;
        if (currentChapter == null)
            return Result<StoryState>.Fail("No chapter in progress");
        
        if (currentChapter.Status != StoryProgressStatus.InProgress)
            return Result<StoryState>.Fail("Current chapter is not in progress");
        
        // Complete the current chapter
        currentChapter.Complete();
        
        // Return state with chapter cleared
        return Result<StoryState>.Ok(state.WithCurrentChapter(null));
    }

    public IReadOnlyList<Domain.Event> GetEventHistory(StoryState state)
    {
        if (state == null)
            return [];
        
        return state.EventHistory;
    }

    public int GetEventCount(StoryState state)
    {
        if (state == null)
            return 0;
        
        return state.EventHistory.Count;
    }
}

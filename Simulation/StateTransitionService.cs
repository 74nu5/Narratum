using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Simulation;

/// <summary>
/// Implementation of state transition service.
/// Validates and applies actions to story states deterministically.
/// </summary>
public class StateTransitionService : IStateTransitionService
{
    private readonly IEnumerable<IStoryRule> _globalRules;
    private readonly IRuleEngine _ruleEngine;

    public StateTransitionService(IEnumerable<IStoryRule>? globalRules = null, IRuleEngine? ruleEngine = null)
    {
        _globalRules = globalRules ?? [];
        _ruleEngine = ruleEngine ?? new RuleEngine();
    }

    public Result<Unit> ValidateAction(StoryState? state, StoryAction? action)
    {
        if (state == null)
            return Result<Unit>.Fail("State cannot be null");
        if (action == null)
            return Result<Unit>.Fail("Action cannot be null");

        // Validate action-specific rules
        return ValidateActionInternal(state, action);
    }

    public Result<StoryState> ApplyAction(StoryState? state, StoryAction? action)
    {
        if (state == null)
            return Result<StoryState>.Fail("State cannot be null");
        if (action == null)
            return Result<StoryState>.Fail("Action cannot be null");

        // Apply action based on type
        return ApplyActionInternal(state, action);
    }

    public Result<StoryState> TransitionState(StoryState? state, StoryAction? action)
    {
        // First validate
        var validationResult = ValidateAction(state, action);
        if (validationResult is Result<Unit>.Failure failure)
            return Result<StoryState>.Fail(failure.Message);

        // Then apply
        return ApplyAction(state, action);
    }

    private Result<Unit> ValidateActionInternal(StoryState state, StoryAction action)
    {
        // First check rules engine
        var ruleResult = _ruleEngine.ValidateAction(state, action);
        if (ruleResult is Result<Unit>.Failure failure)
            return failure;

        // Then check action-specific validation
        return action switch
        {
            MoveCharacterAction moveAction => ValidateMoveCharacter(state, moveAction),
            EndChapterAction endChapterAction => ValidateEndChapter(state, endChapterAction),
            TriggerEncounterAction encounterAction => ValidateTriggerEncounter(state, encounterAction),
            RecordCharacterDeathAction deathAction => ValidateRecordCharacterDeath(state, deathAction),
            AdvanceTimeAction timeAction => ValidateAdvanceTime(state, timeAction),
            UpdateRelationshipAction relAction => ValidateUpdateRelationship(state, relAction),
            RecordRevelationAction revAction => ValidateRecordRevelation(state, revAction),
            _ => Result<Unit>.Fail($"Unknown action type: {action.GetType().Name}")
        };
    }

    private Result<StoryState> ApplyActionInternal(StoryState state, StoryAction action)
    {
        return action switch
        {
            MoveCharacterAction moveAction => ApplyMoveCharacter(state, moveAction),
            EndChapterAction endChapterAction => ApplyEndChapter(state, endChapterAction),
            TriggerEncounterAction encounterAction => ApplyTriggerEncounter(state, encounterAction),
            RecordCharacterDeathAction deathAction => ApplyRecordCharacterDeath(state, deathAction),
            AdvanceTimeAction timeAction => ApplyAdvanceTime(state, timeAction),
            UpdateRelationshipAction relAction => ApplyUpdateRelationship(state, relAction),
            RecordRevelationAction revAction => ApplyRecordRevelation(state, revAction),
            _ => Result<StoryState>.Fail($"Unknown action type: {action.GetType().Name}")
        };
    }

    // Validation methods
    private Result<Unit> ValidateMoveCharacter(StoryState state, MoveCharacterAction action)
    {
        var character = state.GetCharacter(action.CharacterId);
        if (character == null)
            return Result<Unit>.Fail($"Character {action.CharacterId.Value} not found");
        
        if (character.VitalStatus == VitalStatus.Dead)
            return Result<Unit>.Fail("Dead characters cannot move");
        
        return Result<Unit>.Ok(default);
    }

    private Result<Unit> ValidateEndChapter(StoryState state, EndChapterAction action)
    {
        if (state.CurrentChapter == null)
            return Result<Unit>.Fail("No chapter in progress");
        
        if (state.CurrentChapter.Id != action.ChapterId)
            return Result<Unit>.Fail("Chapter mismatch");
        
        return Result<Unit>.Ok(default);
    }

    private Result<Unit> ValidateTriggerEncounter(StoryState state, TriggerEncounterAction action)
    {
        var char1 = state.GetCharacter(action.Character1Id);
        var char2 = state.GetCharacter(action.Character2Id);
        
        if (char1 == null || char2 == null)
            return Result<Unit>.Fail("One or both characters not found");
        
        if (char1.VitalStatus == VitalStatus.Dead || char2.VitalStatus == VitalStatus.Dead)
            return Result<Unit>.Fail("Dead characters cannot encounter");
        
        return Result<Unit>.Ok(default);
    }

    private Result<Unit> ValidateRecordCharacterDeath(StoryState state, RecordCharacterDeathAction action)
    {
        var character = state.GetCharacter(action.CharacterId);
        if (character == null)
            return Result<Unit>.Fail($"Character {action.CharacterId.Value} not found");
        
        if (character.VitalStatus == VitalStatus.Dead)
            return Result<Unit>.Fail("Character is already dead");
        
        return Result<Unit>.Ok(default);
    }

    private Result<Unit> ValidateAdvanceTime(StoryState state, AdvanceTimeAction action)
    {
        if (action.Duration < TimeSpan.Zero)
            return Result<Unit>.Fail("Cannot go back in time");
        
        return Result<Unit>.Ok(default);
    }

    private Result<Unit> ValidateUpdateRelationship(StoryState state, UpdateRelationshipAction action)
    {
        var char1 = state.GetCharacter(action.Character1Id);
        var char2 = state.GetCharacter(action.Character2Id);
        
        if (char1 == null || char2 == null)
            return Result<Unit>.Fail("One or both characters not found");
        
        if (action.Character1Id == action.Character2Id)
            return Result<Unit>.Fail("Cannot create relationship with self");
        
        return Result<Unit>.Ok(default);
    }

    private Result<Unit> ValidateRecordRevelation(StoryState state, RecordRevelationAction action)
    {
        var character = state.GetCharacter(action.CharacterId);
        if (character == null)
            return Result<Unit>.Fail($"Character {action.CharacterId.Value} not found");
        
        return Result<Unit>.Ok(default);
    }

    // Application methods
    private Result<StoryState> ApplyMoveCharacter(StoryState state, MoveCharacterAction action)
    {
        var character = state.GetCharacter(action.CharacterId);
        if (character == null)
            return Result<StoryState>.Fail("Character not found");
        
        var newCharacterState = character.MoveTo(action.ToLocationId);
        var newState = state.WithCharacter(newCharacterState);
        
        // Generate movement event
        var moveEvent = new CharacterMovedEvent(action.CharacterId, character.CurrentLocationId ?? Id.New(), action.ToLocationId);
        newState = newState.WithEvent(moveEvent);
        
        return Result<StoryState>.Ok(newState);
    }

    private Result<StoryState> ApplyEndChapter(StoryState state, EndChapterAction action)
    {
        if (state.CurrentChapter == null)
            return Result<StoryState>.Fail("No chapter to end");
        
        // Complete current chapter
        state.CurrentChapter.Complete();
        
        // Return state with chapter completed
        return Result<StoryState>.Ok(state.WithCurrentChapter(null));
    }

    private Result<StoryState> ApplyTriggerEncounter(StoryState state, TriggerEncounterAction action)
    {
        var encounterEvent = new CharacterEncounterEvent(action.Character1Id, action.Character2Id, action.LocationId);
        var newState = state.WithEvent(encounterEvent);
        
        return Result<StoryState>.Ok(newState);
    }

    private Result<StoryState> ApplyRecordCharacterDeath(StoryState state, RecordCharacterDeathAction action)
    {
        var character = state.GetCharacter(action.CharacterId);
        if (character == null)
            return Result<StoryState>.Fail("Character not found");
        
        // Update character state
        var updatedCharacter = character.WithVitalStatus(VitalStatus.Dead);
        var newState = state.WithCharacter(updatedCharacter);
        
        // Generate death event
        var deathEvent = new CharacterDeathEvent(action.CharacterId, action.LocationId, action.Cause);
        newState = newState.WithEvent(deathEvent);
        
        return Result<StoryState>.Ok(newState);
    }

    private Result<StoryState> ApplyAdvanceTime(StoryState state, AdvanceTimeAction action)
    {
        var newWorldState = state.WorldState.AdvanceTime(action.Duration);
        var newState = state with { WorldState = newWorldState };
        
        return Result<StoryState>.Ok(newState);
    }

    private Result<StoryState> ApplyUpdateRelationship(StoryState state, UpdateRelationshipAction action)
    {
        // For now, just return success - relationships are stored in domain entities
        // In a full implementation, we'd update character states
        return Result<StoryState>.Ok(state);
    }

    private Result<StoryState> ApplyRecordRevelation(StoryState state, RecordRevelationAction action)
    {
        var character = state.GetCharacter(action.CharacterId);
        if (character == null)
            return Result<StoryState>.Fail("Character not found");
        
        // Update character with known fact
        var updatedCharacter = character.WithKnownFact(action.RevelationContent);
        var newState = state.WithCharacter(updatedCharacter);
        
        // Generate revelation event
        var revealEvent = new RevelationEvent(action.CharacterId, action.RevelationContent);
        newState = newState.WithEvent(revealEvent);
        
        return Result<StoryState>.Ok(newState);
    }
}

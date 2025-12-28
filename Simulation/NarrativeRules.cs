using Narratum.Core;
using Narratum.State;
using Narratum.Simulation;

namespace Narratum.Domain;

/// <summary>
/// Base class for narrative rules.
/// Provides common functionality for all rules.
/// </summary>
public abstract class NarrativeRuleBase : IRule
{
    public abstract string RuleId { get; }
    public abstract string RuleName { get; }

    public abstract Result<Unit> Evaluate(StoryState state);

    public virtual Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        // Default: just evaluate the rule against state
        return Evaluate(state);
    }

    /// <summary>
    /// Helper to create error results.
    /// </summary>
    protected Result<Unit> Fail(string message)
        => Result<Unit>.Fail(message);

    /// <summary>
    /// Helper to create success results.
    /// </summary>
    protected Result<Unit> Ok()
        => Result<Unit>.Ok(Unit.Default());
}

/// <summary>
/// Rule: A character must be alive to act.
/// </summary>
public class CharacterMustBeAliveRule : NarrativeRuleBase
{
    public override string RuleId => "CHAR_ALIVE";
    public override string RuleName => "Character Must Be Alive";

    public override Result<Unit> Evaluate(StoryState state)
    {
        // State itself is always valid - check applied in actions
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is MoveCharacterAction moveAction)
        {
            var character = state.GetCharacter(moveAction.CharacterId);
            if (character?.VitalStatus == VitalStatus.Dead)
                return Fail($"Character {character.Name} is dead and cannot move");
        }
        else if (action is TriggerEncounterAction encounterAction)
        {
            var char1 = state.GetCharacter(encounterAction.Character1Id);
            var char2 = state.GetCharacter(encounterAction.Character2Id);

            if (char1?.VitalStatus == VitalStatus.Dead)
                return Fail($"Character {char1.Name} is dead and cannot participate in encounters");
            if (char2?.VitalStatus == VitalStatus.Dead)
                return Fail($"Character {char2.Name} is dead and cannot participate in encounters");
        }
        else if (action is RecordRevelationAction revelationAction)
        {
            var character = state.GetCharacter(revelationAction.CharacterId);
            if (character?.VitalStatus == VitalStatus.Dead)
                return Fail($"Character {character.Name} is dead and cannot learn revelations");
        }

        return Ok();
    }
}

/// <summary>
/// Rule: Locations referenced in actions must exist.
/// </summary>
public class LocationMustExistRule : NarrativeRuleBase
{
    public override string RuleId => "LOC_EXISTS";
    public override string RuleName => "Location Must Exist";

    public override Result<Unit> Evaluate(StoryState state)
    {
        // State itself is always valid
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is MoveCharacterAction moveAction)
        {
            // For now, we don't have a location list in state, so we can't validate
            // In a real implementation, StoryWorld would have all valid locations
            return Ok();
        }
        else if (action is TriggerEncounterAction encounterAction)
        {
            // Similar - would validate location exists
            return Ok();
        }

        return Ok();
    }
}

/// <summary>
/// Rule: Characters referenced in actions must exist.
/// </summary>
public class CharacterMustExistRule : NarrativeRuleBase
{
    public override string RuleId => "CHAR_EXISTS";
    public override string RuleName => "Character Must Exist";

    public override Result<Unit> Evaluate(StoryState state)
    {
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is MoveCharacterAction moveAction)
        {
            if (state.GetCharacter(moveAction.CharacterId) == null)
                return Fail($"Character {moveAction.CharacterId} does not exist");
        }
        else if (action is TriggerEncounterAction encounterAction)
        {
            if (state.GetCharacter(encounterAction.Character1Id) == null)
                return Fail($"Character {encounterAction.Character1Id} does not exist");
            if (state.GetCharacter(encounterAction.Character2Id) == null)
                return Fail($"Character {encounterAction.Character2Id} does not exist");
        }
        else if (action is RecordCharacterDeathAction deathAction)
        {
            if (state.GetCharacter(deathAction.CharacterId) == null)
                return Fail($"Character {deathAction.CharacterId} does not exist");
        }
        else if (action is UpdateRelationshipAction relationshipAction)
        {
            if (state.GetCharacter(relationshipAction.Character1Id) == null)
                return Fail($"Character {relationshipAction.Character1Id} does not exist");
            if (state.GetCharacter(relationshipAction.Character2Id) == null)
                return Fail($"Character {relationshipAction.Character2Id} does not exist");
        }
        else if (action is RecordRevelationAction revelationAction)
        {
            if (state.GetCharacter(revelationAction.CharacterId) == null)
                return Fail($"Character {revelationAction.CharacterId} does not exist");
        }

        return Ok();
    }
}

/// <summary>
/// Rule: Time must progress monotonically (never go backward).
/// </summary>
public class TimeMonotonicityRule : NarrativeRuleBase
{
    public override string RuleId => "TIME_MONOTONIC";
    public override string RuleName => "Time Must Be Monotonic";

    public override Result<Unit> Evaluate(StoryState state)
    {
        // Current state is always valid
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is AdvanceTimeAction advanceAction)
        {
            if (advanceAction.Duration < TimeSpan.Zero)
                return Fail("Time cannot go backward");
            if (advanceAction.Duration == TimeSpan.Zero)
                return Fail("Time duration must be positive");
        }

        return Ok();
    }
}

/// <summary>
/// Rule: No self-relationships allowed.
/// </summary>
public class NoSelfRelationshipRule : NarrativeRuleBase
{
    public override string RuleId => "NO_SELF_REL";
    public override string RuleName => "No Self-Relationships";

    public override Result<Unit> Evaluate(StoryState state)
    {
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is UpdateRelationshipAction relationshipAction)
        {
            if (relationshipAction.Character1Id == relationshipAction.Character2Id)
                return Fail("A character cannot have a relationship with themselves");
        }

        return Ok();
    }
}

/// <summary>
/// Rule: A character cannot die twice.
/// </summary>
public class CannotDieTwiceRule : NarrativeRuleBase
{
    public override string RuleId => "CANT_DIE_TWICE";
    public override string RuleName => "Cannot Die Twice";

    public override Result<Unit> Evaluate(StoryState state)
    {
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is RecordCharacterDeathAction deathAction)
        {
            var character = state.GetCharacter(deathAction.CharacterId);
            if (character?.VitalStatus == VitalStatus.Dead)
                return Fail($"Character {character.Name} is already dead and cannot die again");
        }

        return Ok();
    }
}

/// <summary>
/// Rule: Character cannot move to same location.
/// </summary>
public class CannotStayInSameLocationRule : NarrativeRuleBase
{
    public override string RuleId => "DIFF_LOCATION";
    public override string RuleName => "Must Move to Different Location";

    public override Result<Unit> Evaluate(StoryState state)
    {
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        if (action is MoveCharacterAction moveAction)
        {
            var character = state.GetCharacter(moveAction.CharacterId);
            if (character?.CurrentLocationId == moveAction.ToLocationId)
                return Fail($"Character {character.Name} is already in this location");
        }

        return Ok();
    }
}

/// <summary>
/// Rule: Encounter requires both characters at same location.
/// </summary>
public class EncounterLocationConsistencyRule : NarrativeRuleBase
{
    public override string RuleId => "ENCOUNTER_LOC";
    public override string RuleName => "Encounter Location Consistency";

    public override Result<Unit> Evaluate(StoryState state)
    {
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        // For Phase 1.4, we don't strictly enforce that characters must be at same location
        // This could be added if needed
        return Ok();
    }
}

/// <summary>
/// Rule: Events are immutable - cannot be modified or deleted.
/// </summary>
public class EventImmutabilityRule : NarrativeRuleBase
{
    public override string RuleId => "EVENT_IMMUTABLE";
    public override string RuleName => "Events Are Immutable";

    public override Result<Unit> Evaluate(StoryState state)
    {
        // Event immutability is enforced by the data structure, not by this rule
        return Ok();
    }

    public override Result<Unit> EvaluateForAction(StoryState state, object? action)
    {
        return Ok();
    }
}

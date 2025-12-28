namespace Narratum.Core;

/// <summary>
/// Base interface for all story rules in the narrative engine.
/// Rules are evaluated deterministically to validate actions and ensure domain invariants.
/// </summary>
public interface IStoryRule
{
    /// <summary>
    /// Gets the name of the rule for logging and debugging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates whether an action is allowed given the current state.
    /// Rules must be deterministic: same inputs always produce the same output.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <param name="action">The action to validate.</param>
    /// <returns>A result indicating whether the action is valid.</returns>
    Result<Unit> Validate(object state, object action);
}

/// <summary>
/// Unit type representing "no value" - used for results that don't return data.
/// </summary>
public readonly struct Unit
{
    /// <summary>
    /// The single instance of Unit.
    /// </summary>
    public static Unit Default() => default;
}

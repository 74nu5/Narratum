using Narratum.Core;
using Narratum.State;

namespace Narratum.Simulation;

/// <summary>
/// Service responsible for validating and applying state transitions.
/// Ensures all rules are satisfied before applying actions to the state.
/// </summary>
public interface IStateTransitionService
{
    /// <summary>
    /// Validates whether an action can be applied to the current state.
    /// Does not modify the state.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <param name="action">The action to validate.</param>
    /// <returns>Success if valid, Failure with reason if not.</returns>
    Result<Unit> ValidateAction(StoryState? state, StoryAction? action);

    /// <summary>
    /// Applies an action to the state and returns the new state.
    /// Validation must be performed before calling this method.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <param name="action">The action to apply.</param>
    /// <returns>New state after applying the action, or Failure if application fails.</returns>
    Result<StoryState> ApplyAction(StoryState? state, StoryAction? action);

    /// <summary>
    /// Validates and applies an action in one step.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <param name="action">The action to apply.</param>
    /// <returns>New state if successful, Failure if validation or application fails.</returns>
    Result<StoryState> TransitionState(StoryState? state, StoryAction? action);
}

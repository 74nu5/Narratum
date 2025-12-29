using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Simulation;

/// <summary>
/// Interface for the rule engine.
/// Evaluates and validates narrative rules.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Validates the current state against all rules.
    /// </summary>
    /// <param name="state">The state to validate.</param>
    /// <returns>Success if all rules satisfied, Failure if any critical rule violated.</returns>
    Result<Unit> ValidateState(StoryState state);

    /// <summary>
    /// Validates an action before applying it.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to validate.</param>
    /// <returns>Success if action is valid, Failure if any rule violated.</returns>
    Result<Unit> ValidateAction(StoryState state, StoryAction? action);

    /// <summary>
    /// Gets all violations in the current state.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <returns>Collection of all violations found.</returns>
    IReadOnlyList<RuleViolation> GetStateViolations(StoryState state);

    /// <summary>
    /// Gets all violations for an action.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to check.</param>
    /// <returns>Collection of violations found for this action.</returns>
    IReadOnlyList<RuleViolation> GetActionViolations(StoryState state, StoryAction? action);

    /// <summary>
    /// Gets the registered rules.
    /// </summary>
    IReadOnlyList<IRule> Rules { get; }
}

/// <summary>
/// Implementation of the rule engine.
/// Evaluates deterministic narrative rules.
/// </summary>
public class RuleEngine : IRuleEngine
{
    private readonly List<IRule> _rules;

    public IReadOnlyList<IRule> Rules => _rules.AsReadOnly();

    public RuleEngine(IEnumerable<IRule>? rules = null)
    {
        _rules = new List<IRule>(rules ?? GetDefaultRules());
    }

    public Result<Unit> ValidateState(StoryState state)
    {
        if (state == null)
            return Result<Unit>.Fail("State cannot be null");

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(state);
            if (result is Result<Unit>.Failure failure)
                return result;
        }

        return Result<Unit>.Ok(Unit.Default());
    }

    public Result<Unit> ValidateAction(StoryState state, StoryAction? action)
    {
        if (state == null)
            return Result<Unit>.Fail("State cannot be null");
        if (action == null)
            return Result<Unit>.Fail("Action cannot be null");

        foreach (var rule in _rules)
        {
            var result = rule.EvaluateForAction(state, action);
            if (result is Result<Unit>.Failure failure)
                return result;
        }

        return Result<Unit>.Ok(Unit.Default());
    }

    public IReadOnlyList<RuleViolation> GetStateViolations(StoryState state)
    {
        if (state == null)
            return new List<RuleViolation>();

        var violations = new List<RuleViolation>();

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(state);
            if (result is Result<Unit>.Failure failure)
            {
                violations.Add(RuleViolation.Error(rule.RuleId, rule.RuleName, failure.Message));
            }
        }

        return violations.AsReadOnly();    }

    public IReadOnlyList<RuleViolation> GetActionViolations(StoryState state, StoryAction? action)
    {
        if (state == null || action == null)
            return new List<RuleViolation>();

        var violations = new List<RuleViolation>();

        foreach (var rule in _rules)
        {
            var result = rule.EvaluateForAction(state, action);
            if (result is Result<Unit>.Failure failure)
            {
                violations.Add(RuleViolation.Error(rule.RuleId, rule.RuleName, failure.Message));
            }
        }

        return violations.AsReadOnly();
    }

    /// <summary>
    /// Gets the default set of narrative rules.
    /// </summary>
    private static IEnumerable<IRule> GetDefaultRules()
    {
        yield return new CharacterMustBeAliveRule();
        yield return new CharacterMustExistRule();
        yield return new LocationMustExistRule();
        yield return new TimeMonotonicityRule();
        yield return new NoSelfRelationshipRule();
        yield return new CannotDieTwiceRule();
        yield return new CannotStayInSameLocationRule();
        yield return new EncounterLocationConsistencyRule();
        yield return new EventImmutabilityRule();
    }
}

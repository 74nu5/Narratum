using Narratum.Core;
using Narratum.State;

namespace Narratum.Domain;

/// <summary>
/// Interface for a narrative rule that validates state and actions.
/// Rules are deterministic and composable.
/// </summary>
public interface IRule
{
    /// <summary>
    /// Unique identifier for this rule.
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Human-readable name of the rule.
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// Evaluates the rule against the current state.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <returns>Result of evaluation - Success if satisfied, Failure if violated.</returns>
    Result<Unit> Evaluate(StoryState state);

    /// <summary>
    /// Evaluates the rule for a specific action on the given state.
    /// </summary>
    /// <param name="state">The current story state.</param>
    /// <param name="action">The action being validated.</param>
    /// <returns>Result of evaluation - Success if valid, Failure if rule violated.</returns>
    Result<Unit> EvaluateForAction(StoryState state, object? action);
}

/// <summary>
/// Generic interface for rules that work with a specific context type.
/// </summary>
/// <typeparam name="TContext">The context type this rule evaluates.</typeparam>
public interface IRule<TContext> : IRule where TContext : class
{
    /// <summary>
    /// Evaluates the rule with a strongly-typed context.
    /// </summary>
    Result<Unit> EvaluateTyped(TContext context);
}

/// <summary>
/// Violation of a narrative rule.
/// </summary>
public record RuleViolation(
    string RuleId,
    string RuleName,
    string Message,
    RuleSeverity Severity = RuleSeverity.Error,
    string? Context = null)
{
    /// <summary>
    /// Creates a new rule violation with error severity.
    /// </summary>
    public static RuleViolation Error(string ruleId, string ruleName, string message, string? context = null)
        => new(ruleId, ruleName, message, RuleSeverity.Error, context);

    /// <summary>
    /// Creates a new rule violation with warning severity.
    /// </summary>
    public static RuleViolation Warning(string ruleId, string ruleName, string message, string? context = null)
        => new(ruleId, ruleName, message, RuleSeverity.Warning, context);

    /// <summary>
    /// Creates a new rule violation with info severity.
    /// </summary>
    public static RuleViolation Info(string ruleId, string ruleName, string message, string? context = null)
        => new(ruleId, ruleName, message, RuleSeverity.Info, context);
}

/// <summary>
/// Severity level of a rule violation.
/// </summary>
public enum RuleSeverity
{
    /// <summary>
    /// Blocks the action completely.
    /// </summary>
    Error,

    /// <summary>
    /// Warning but may proceed.
    /// </summary>
    Warning,

    /// <summary>
    /// Informational only.
    /// </summary>
    Info
}

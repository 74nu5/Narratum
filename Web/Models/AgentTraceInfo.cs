namespace Narratum.Web.Models;

/// <summary>
/// One agent's contribution to a generated page, surfaced only in Expert mode.
/// The user normally sees just the final narrative; experts can inspect what each
/// specialized agent (Summary, Narrator, Consistency, Character) produced.
/// </summary>
public record AgentTraceInfo(
    string Agent,
    string Role,
    string Output,
    double DurationMs);

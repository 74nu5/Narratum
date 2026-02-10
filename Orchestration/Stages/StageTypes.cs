using Narratum.Core;
using Narratum.State;
using Narratum.Memory;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Type d'agent disponible dans le système.
/// </summary>
public enum AgentType
{
    /// <summary>
    /// Agent de résumé - génère des résumés factuels.
    /// </summary>
    Summary,

    /// <summary>
    /// Agent narrateur - génère le texte narratif principal.
    /// </summary>
    Narrator,

    /// <summary>
    /// Agent de personnage - génère dialogues et réactions.
    /// </summary>
    Character,

    /// <summary>
    /// Agent de cohérence - vérifie la cohérence logique.
    /// </summary>
    Consistency
}

/// <summary>
/// Ordre d'exécution des prompts.
/// </summary>
public enum ExecutionOrder
{
    /// <summary>
    /// Exécution séquentielle - un agent après l'autre.
    /// </summary>
    Sequential,

    /// <summary>
    /// Exécution parallèle - tous les agents en même temps.
    /// </summary>
    Parallel,

    /// <summary>
    /// Exécution conditionnelle - selon les résultats précédents.
    /// </summary>
    Conditional
}

/// <summary>
/// Priorité d'un prompt.
/// </summary>
public enum PromptPriority
{
    /// <summary>
    /// Doit s'exécuter obligatoirement.
    /// </summary>
    Required,

    /// <summary>
    /// Peut être ignoré en cas d'erreur.
    /// </summary>
    Optional,

    /// <summary>
    /// Exécuté uniquement si le principal échoue.
    /// </summary>
    Fallback
}

/// <summary>
/// Sévérité d'une erreur de validation.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Peut être ignoré.
    /// </summary>
    Minor,

    /// <summary>
    /// Devrait être corrigé.
    /// </summary>
    Major,

    /// <summary>
    /// Doit être corrigé absolument.
    /// </summary>
    Critical
}

/// <summary>
/// Type de changement d'état.
/// </summary>
public enum StateChangeType
{
    CharacterMoved,
    CharacterStatusChanged,
    RelationshipUpdated,
    FactRevealed,
    TimeAdvanced,
    EventOccurred
}

/// <summary>
/// Contexte d'un personnage pour la génération narrative.
/// </summary>
public sealed record CharacterContext(
    Id CharacterId,
    string Name,
    VitalStatus Status,
    IReadOnlySet<string> KnownFacts,
    IReadOnlySet<string>? Traits = null,
    string? CurrentMood = null,
    Id? CurrentLocationId = null)
{
    /// <summary>
    /// Traits du personnage (vide si non défini).
    /// </summary>
    public IReadOnlySet<string> CharacterTraits => Traits ?? new HashSet<string>();

    public static CharacterContext FromCharacterState(CharacterState state)
        => new(
            state.CharacterId,
            state.Name,
            state.VitalStatus,
            state.KnownFacts,
            null, // Traits - can be added later
            null, // CurrentMood - can be added later
            state.CurrentLocationId);
}

/// <summary>
/// Contexte d'un lieu pour la génération narrative.
/// </summary>
public sealed record LocationContext(
    Id LocationId,
    string Name,
    string Description,
    IReadOnlySet<Id> PresentCharacterIds)
{
    public static LocationContext Create(Id locationId, string name, string description)
        => new(locationId, name, description, new HashSet<Id>());

    public LocationContext WithCharacter(Id characterId)
    {
        var newSet = new HashSet<Id>(PresentCharacterIds) { characterId };
        return this with { PresentCharacterIds = newSet };
    }
}

/// <summary>
/// Contexte narratif enrichi pour les stages du pipeline.
/// Contient toutes les informations nécessaires pour la génération.
/// </summary>
public sealed record NarrativeContext
{
    public Id ContextId { get; }
    public StoryState State { get; }
    public IReadOnlyList<Memorandum> RecentMemoria { get; }
    public CanonicalState? CanonicalState { get; }
    public IReadOnlyList<CharacterContext> ActiveCharacters { get; }
    public LocationContext? CurrentLocation { get; }
    public IReadOnlyList<object> RecentEvents { get; }
    public string? RecentSummary { get; }
    public DateTime ContextBuiltAt { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public NarrativeContext(
        StoryState state,
        IReadOnlyList<Memorandum>? recentMemoria = null,
        CanonicalState? canonicalState = null,
        IEnumerable<CharacterContext>? activeCharacters = null,
        LocationContext? currentLocation = null,
        IEnumerable<object>? recentEvents = null,
        string? recentSummary = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ContextId = Id.New();
        State = state ?? throw new ArgumentNullException(nameof(state));
        RecentMemoria = recentMemoria ?? Array.Empty<Memorandum>();
        CanonicalState = canonicalState;
        ActiveCharacters = (activeCharacters?.ToList() ?? new List<CharacterContext>()).AsReadOnly();
        CurrentLocation = currentLocation;
        RecentEvents = (recentEvents?.ToList() ?? new List<object>()).AsReadOnly();
        RecentSummary = recentSummary;
        ContextBuiltAt = DateTime.UtcNow;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    public static NarrativeContext CreateMinimal(StoryState state)
        => new(state);
}

/// <summary>
/// Prompt pour un agent spécifique.
/// </summary>
public sealed record AgentPrompt(
    AgentType TargetAgent,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyDictionary<string, string> Variables,
    PromptPriority Priority = PromptPriority.Required)
{
    public static AgentPrompt Create(AgentType agent, string systemPrompt, string userPrompt)
        => new(agent, systemPrompt, userPrompt, new Dictionary<string, string>());

    public AgentPrompt WithVariable(string key, string value)
    {
        var newVars = new Dictionary<string, string>(Variables) { [key] = value };
        return this with { Variables = newVars };
    }
}

/// <summary>
/// Ensemble de prompts à exécuter.
/// </summary>
public sealed record PromptSet(
    IReadOnlyList<AgentPrompt> Prompts,
    ExecutionOrder Order = ExecutionOrder.Sequential)
{
    public static PromptSet Single(AgentPrompt prompt)
        => new(new[] { prompt });

    public static PromptSet Sequential(params AgentPrompt[] prompts)
        => new(prompts, ExecutionOrder.Sequential);

    public static PromptSet Parallel(params AgentPrompt[] prompts)
        => new(prompts, ExecutionOrder.Parallel);

    public bool HasPromptFor(AgentType agent)
        => Prompts.Any(p => p.TargetAgent == agent);

    public AgentPrompt? GetPromptFor(AgentType agent)
        => Prompts.FirstOrDefault(p => p.TargetAgent == agent);
}

/// <summary>
/// Réponse d'un agent.
/// </summary>
public sealed record AgentResponse(
    AgentType Agent,
    string Content,
    bool Success,
    string? ErrorMessage,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object> Metadata)
{
    public static AgentResponse CreateSuccess(AgentType agent, string content, TimeSpan duration)
        => new(agent, content, true, null, duration, new Dictionary<string, object>());

    public static AgentResponse CreateFailure(AgentType agent, string error, TimeSpan duration)
        => new(agent, string.Empty, false, error, duration, new Dictionary<string, object>());

    public AgentResponse WithMetadata(string key, object value)
    {
        var newMeta = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMeta };
    }
}

/// <summary>
/// Sortie brute de l'exécution des agents.
/// </summary>
public sealed record RawOutput(
    IReadOnlyDictionary<AgentType, AgentResponse> Responses,
    DateTime GeneratedAt,
    TimeSpan TotalDuration)
{
    public static RawOutput Create(IEnumerable<AgentResponse> responses, TimeSpan duration)
    {
        var dict = responses.ToDictionary(r => r.Agent, r => r);
        return new RawOutput(dict, DateTime.UtcNow, duration);
    }

    public AgentResponse? GetResponse(AgentType agent)
        => Responses.GetValueOrDefault(agent);

    public bool HasSuccessfulResponse(AgentType agent)
        => Responses.TryGetValue(agent, out var r) && r.Success;

    public string? GetContent(AgentType agent)
        => GetResponse(agent)?.Content;

    public bool AllSuccessful => Responses.Values.All(r => r.Success);
}

/// <summary>
/// Erreur de validation.
/// </summary>
public sealed record ValidationError(
    string Message,
    ErrorSeverity Severity,
    string? SuggestedFix = null,
    string? Context = null)
{
    public static ValidationError Critical(string message, string? fix = null)
        => new(message, ErrorSeverity.Critical, fix);

    public static ValidationError Major(string message, string? fix = null)
        => new(message, ErrorSeverity.Major, fix);

    public static ValidationError Minor(string message)
        => new(message, ErrorSeverity.Minor);
}

/// <summary>
/// Avertissement de validation.
/// </summary>
public sealed record ValidationWarning(
    string Message,
    string? Context = null);

/// <summary>
/// Résultat de validation.
/// </summary>
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings,
    IReadOnlyDictionary<string, object> Metadata)
{
    public static ValidationResult Valid()
        => new(true, Array.Empty<ValidationError>(), Array.Empty<ValidationWarning>(),
            new Dictionary<string, object>());

    public static ValidationResult Invalid(params ValidationError[] errors)
        => new(false, errors, Array.Empty<ValidationWarning>(), new Dictionary<string, object>());

    public static ValidationResult Invalid(string errorMessage)
        => Invalid(ValidationError.Critical(errorMessage));

    public static ValidationResult WithWarnings(params ValidationWarning[] warnings)
        => new(true, Array.Empty<ValidationError>(), warnings, new Dictionary<string, object>());

    public bool HasCriticalErrors => Errors.Any(e => e.Severity == ErrorSeverity.Critical);
    public bool HasWarnings => Warnings.Count > 0;

    public IEnumerable<string> ErrorMessages => Errors.Select(e => e.Message);
}

/// <summary>
/// Changement d'état produit par la génération narrative.
/// </summary>
public sealed record StateChange(
    StateChangeType Type,
    Id EntityId,
    string Description,
    object? OldValue = null,
    object? NewValue = null)
{
    public static StateChange CharacterMoved(Id characterId, Id? fromLocation, Id toLocation)
        => new(StateChangeType.CharacterMoved, characterId,
            $"Character moved", fromLocation, toLocation);

    public static StateChange FactRevealed(Id characterId, string fact)
        => new(StateChangeType.FactRevealed, characterId, fact);

    public static StateChange TimeAdvanced(TimeSpan duration)
        => new(StateChangeType.TimeAdvanced, Id.New(), $"Time advanced by {duration}");
}

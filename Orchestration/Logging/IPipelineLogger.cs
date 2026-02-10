using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Logging;

/// <summary>
/// Type d'événement dans le pipeline.
/// </summary>
public enum PipelineEventType
{
    /// <summary>
    /// Le pipeline a démarré.
    /// </summary>
    PipelineStarted,

    /// <summary>
    /// Le pipeline s'est terminé avec succès.
    /// </summary>
    PipelineCompleted,

    /// <summary>
    /// Le pipeline a échoué avec une erreur.
    /// </summary>
    PipelineError,

    /// <summary>
    /// Une étape du pipeline a démarré.
    /// </summary>
    StageStarted,

    /// <summary>
    /// Une étape du pipeline s'est terminée avec succès.
    /// </summary>
    StageCompleted,

    /// <summary>
    /// Une étape du pipeline a échoué.
    /// </summary>
    StageError,

    /// <summary>
    /// Un agent a été appelé.
    /// </summary>
    AgentCalled,

    /// <summary>
    /// Un agent a répondu.
    /// </summary>
    AgentResponded,

    /// <summary>
    /// Une validation a été effectuée.
    /// </summary>
    ValidationPerformed,

    /// <summary>
    /// Une tentative de retry a été effectuée.
    /// </summary>
    RetryAttempted,

    /// <summary>
    /// Un événement de diagnostic/debug.
    /// </summary>
    Debug,

    /// <summary>
    /// Un avertissement non-bloquant.
    /// </summary>
    Warning
}

/// <summary>
/// Événement enregistré dans le pipeline.
/// </summary>
public sealed record PipelineEvent(
    Guid EventId,
    Guid PipelineId,
    DateTime Timestamp,
    PipelineEventType Type,
    string Description,
    TimeSpan? Duration = null,
    IReadOnlyDictionary<string, object>? Data = null)
{
    /// <summary>
    /// Crée un nouvel événement de pipeline.
    /// </summary>
    public static PipelineEvent Create(
        Guid pipelineId,
        PipelineEventType type,
        string description,
        TimeSpan? duration = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return new PipelineEvent(
            Guid.NewGuid(),
            pipelineId,
            DateTime.UtcNow,
            type,
            description,
            duration,
            data);
    }

    /// <summary>
    /// Crée un événement de démarrage de pipeline.
    /// </summary>
    public static PipelineEvent PipelineStarted(Guid pipelineId, NarrativeIntent intent)
    {
        return Create(
            pipelineId,
            PipelineEventType.PipelineStarted,
            $"Pipeline started with intent: {intent.Type}",
            data: new Dictionary<string, object>
            {
                ["intent_type"] = intent.Type.ToString(),
                ["intent_id"] = intent.Id.ToString()
            });
    }

    /// <summary>
    /// Crée un événement de complétion de pipeline.
    /// </summary>
    public static PipelineEvent PipelineCompleted(Guid pipelineId, TimeSpan duration)
    {
        return Create(
            pipelineId,
            PipelineEventType.PipelineCompleted,
            $"Pipeline completed successfully in {duration.TotalMilliseconds:F0}ms",
            duration);
    }

    /// <summary>
    /// Crée un événement d'erreur de pipeline.
    /// </summary>
    public static PipelineEvent PipelineError(Guid pipelineId, Exception exception)
    {
        return Create(
            pipelineId,
            PipelineEventType.PipelineError,
            $"Pipeline failed: {exception.Message}",
            data: new Dictionary<string, object>
            {
                ["exception_type"] = exception.GetType().Name,
                ["exception_message"] = exception.Message,
                ["stack_trace"] = exception.StackTrace ?? string.Empty
            });
    }

    /// <summary>
    /// Crée un événement de démarrage d'étape.
    /// </summary>
    public static PipelineEvent StageStarted(Guid pipelineId, string stageName)
    {
        return Create(
            pipelineId,
            PipelineEventType.StageStarted,
            $"Stage '{stageName}' started",
            data: new Dictionary<string, object> { ["stage_name"] = stageName });
    }

    /// <summary>
    /// Crée un événement de complétion d'étape.
    /// </summary>
    public static PipelineEvent StageCompleted(Guid pipelineId, string stageName, TimeSpan duration)
    {
        return Create(
            pipelineId,
            PipelineEventType.StageCompleted,
            $"Stage '{stageName}' completed in {duration.TotalMilliseconds:F0}ms",
            duration,
            new Dictionary<string, object> { ["stage_name"] = stageName });
    }

    /// <summary>
    /// Crée un événement d'erreur d'étape.
    /// </summary>
    public static PipelineEvent StageError(Guid pipelineId, string stageName, string error)
    {
        return Create(
            pipelineId,
            PipelineEventType.StageError,
            $"Stage '{stageName}' failed: {error}",
            data: new Dictionary<string, object>
            {
                ["stage_name"] = stageName,
                ["error"] = error
            });
    }

    /// <summary>
    /// Crée un événement d'appel d'agent.
    /// </summary>
    public static PipelineEvent AgentCalled(Guid pipelineId, AgentType agent, int promptLength)
    {
        return Create(
            pipelineId,
            PipelineEventType.AgentCalled,
            $"Agent '{agent}' called with prompt of {promptLength} chars",
            data: new Dictionary<string, object>
            {
                ["agent_type"] = agent.ToString(),
                ["prompt_length"] = promptLength
            });
    }

    /// <summary>
    /// Crée un événement de réponse d'agent.
    /// </summary>
    public static PipelineEvent AgentResponded(
        Guid pipelineId,
        AgentType agent,
        bool success,
        int responseLength,
        TimeSpan duration)
    {
        return Create(
            pipelineId,
            PipelineEventType.AgentResponded,
            success
                ? $"Agent '{agent}' responded with {responseLength} chars in {duration.TotalMilliseconds:F0}ms"
                : $"Agent '{agent}' failed after {duration.TotalMilliseconds:F0}ms",
            duration,
            new Dictionary<string, object>
            {
                ["agent_type"] = agent.ToString(),
                ["success"] = success,
                ["response_length"] = responseLength
            });
    }

    /// <summary>
    /// Crée un événement de validation.
    /// </summary>
    public static PipelineEvent ValidationPerformed(
        Guid pipelineId,
        bool isValid,
        int errorCount,
        int warningCount)
    {
        return Create(
            pipelineId,
            PipelineEventType.ValidationPerformed,
            isValid
                ? $"Validation passed (warnings: {warningCount})"
                : $"Validation failed with {errorCount} error(s) and {warningCount} warning(s)",
            data: new Dictionary<string, object>
            {
                ["is_valid"] = isValid,
                ["error_count"] = errorCount,
                ["warning_count"] = warningCount
            });
    }

    /// <summary>
    /// Crée un événement de retry.
    /// </summary>
    public static PipelineEvent RetryAttempted(
        Guid pipelineId,
        int attemptNumber,
        IEnumerable<string> errors)
    {
        return Create(
            pipelineId,
            PipelineEventType.RetryAttempted,
            $"Retry attempt #{attemptNumber}",
            data: new Dictionary<string, object>
            {
                ["attempt_number"] = attemptNumber,
                ["errors"] = errors.ToList()
            });
    }
}

/// <summary>
/// Interface pour le logging du pipeline d'orchestration.
///
/// Permet de tracer tous les événements du pipeline de manière
/// structurée et consultable.
/// </summary>
public interface IPipelineLogger
{
    /// <summary>
    /// Enregistre le démarrage d'un pipeline.
    /// </summary>
    void LogPipelineStart(Guid pipelineId, NarrativeIntent intent);

    /// <summary>
    /// Enregistre la complétion réussie d'un pipeline.
    /// </summary>
    void LogPipelineComplete(Guid pipelineId, TimeSpan duration);

    /// <summary>
    /// Enregistre une erreur fatale du pipeline.
    /// </summary>
    void LogPipelineError(Guid pipelineId, Exception exception);

    /// <summary>
    /// Enregistre le démarrage d'une étape.
    /// </summary>
    void LogStageStart(Guid pipelineId, string stageName);

    /// <summary>
    /// Enregistre la complétion d'une étape.
    /// </summary>
    void LogStageComplete(Guid pipelineId, string stageName, TimeSpan duration);

    /// <summary>
    /// Enregistre l'échec d'une étape.
    /// </summary>
    void LogStageFailure(Guid pipelineId, string stageName, string error);

    /// <summary>
    /// Enregistre un appel à un agent.
    /// </summary>
    void LogAgentCall(Guid pipelineId, AgentType agent, string prompt);

    /// <summary>
    /// Enregistre la réponse d'un agent.
    /// </summary>
    void LogAgentResponse(Guid pipelineId, AgentType agent, AgentResponse response);

    /// <summary>
    /// Enregistre une tentative de retry.
    /// </summary>
    void LogRetry(Guid pipelineId, int attemptNumber, IEnumerable<string> errors);

    /// <summary>
    /// Enregistre un résultat de validation.
    /// </summary>
    void LogValidation(Guid pipelineId, ValidationResult result);

    /// <summary>
    /// Enregistre un message de debug.
    /// </summary>
    void LogDebug(Guid pipelineId, string message, IReadOnlyDictionary<string, object>? data = null);

    /// <summary>
    /// Enregistre un avertissement.
    /// </summary>
    void LogWarning(Guid pipelineId, string message, IReadOnlyDictionary<string, object>? data = null);

    /// <summary>
    /// Récupère l'historique des événements pour un pipeline.
    /// </summary>
    IReadOnlyList<PipelineEvent> GetPipelineHistory(Guid pipelineId);

    /// <summary>
    /// Récupère tous les événements enregistrés.
    /// </summary>
    IReadOnlyList<PipelineEvent> GetAllEvents();

    /// <summary>
    /// Récupère les événements d'un type spécifique.
    /// </summary>
    IReadOnlyList<PipelineEvent> GetEventsByType(PipelineEventType type);

    /// <summary>
    /// Efface tous les événements.
    /// </summary>
    void Clear();
}

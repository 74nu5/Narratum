using Microsoft.Extensions.Logging;
using Narratum.Orchestration.Models;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Logging;

/// <summary>
/// Implémentation du logger de pipeline.
///
/// Enregistre tous les événements du pipeline dans une collection
/// en mémoire et les transmet également à un ILogger standard.
/// </summary>
public sealed class PipelineLogger : IPipelineLogger
{
    private readonly List<PipelineEvent> _events = new();
    private readonly object _lock = new();
    private readonly ILogger<PipelineLogger>? _logger;
    private readonly PipelineLoggerConfig _config;

    public PipelineLogger(
        ILogger<PipelineLogger>? logger = null,
        PipelineLoggerConfig? config = null)
    {
        _logger = logger;
        _config = config ?? PipelineLoggerConfig.Default;
    }

    public void LogPipelineStart(Guid pipelineId, NarrativeIntent intent)
    {
        var evt = PipelineEvent.PipelineStarted(pipelineId, intent);
        RecordEvent(evt);

        _logger?.LogInformation(
            "Pipeline {PipelineId} started with intent {IntentType}",
            pipelineId, intent.Type);
    }

    public void LogPipelineComplete(Guid pipelineId, TimeSpan duration)
    {
        var evt = PipelineEvent.PipelineCompleted(pipelineId, duration);
        RecordEvent(evt);

        _logger?.LogInformation(
            "Pipeline {PipelineId} completed in {Duration}ms",
            pipelineId, duration.TotalMilliseconds);
    }

    public void LogPipelineError(Guid pipelineId, Exception exception)
    {
        var evt = PipelineEvent.PipelineError(pipelineId, exception);
        RecordEvent(evt);

        _logger?.LogError(
            exception,
            "Pipeline {PipelineId} failed: {ErrorMessage}",
            pipelineId, exception.Message);
    }

    public void LogStageStart(Guid pipelineId, string stageName)
    {
        var evt = PipelineEvent.StageStarted(pipelineId, stageName);
        RecordEvent(evt);

        _logger?.LogDebug(
            "Pipeline {PipelineId}: Stage '{StageName}' started",
            pipelineId, stageName);
    }

    public void LogStageComplete(Guid pipelineId, string stageName, TimeSpan duration)
    {
        var evt = PipelineEvent.StageCompleted(pipelineId, stageName, duration);
        RecordEvent(evt);

        _logger?.LogDebug(
            "Pipeline {PipelineId}: Stage '{StageName}' completed in {Duration}ms",
            pipelineId, stageName, duration.TotalMilliseconds);
    }

    public void LogStageFailure(Guid pipelineId, string stageName, string error)
    {
        var evt = PipelineEvent.StageError(pipelineId, stageName, error);
        RecordEvent(evt);

        _logger?.LogWarning(
            "Pipeline {PipelineId}: Stage '{StageName}' failed: {Error}",
            pipelineId, stageName, error);
    }

    public void LogAgentCall(Guid pipelineId, AgentType agent, string prompt)
    {
        var evt = PipelineEvent.AgentCalled(pipelineId, agent, prompt.Length);
        RecordEvent(evt);

        if (_config.LogPromptContent)
        {
            _logger?.LogDebug(
                "Pipeline {PipelineId}: Agent '{Agent}' called with prompt: {Prompt}",
                pipelineId, agent, TruncateForLog(prompt));
        }
        else
        {
            _logger?.LogDebug(
                "Pipeline {PipelineId}: Agent '{Agent}' called with {PromptLength} chars",
                pipelineId, agent, prompt.Length);
        }
    }

    public void LogAgentResponse(Guid pipelineId, AgentType agent, AgentResponse response)
    {
        var evt = PipelineEvent.AgentResponded(
            pipelineId,
            agent,
            response.Success,
            response.Content.Length,
            response.Duration);
        RecordEvent(evt);

        if (response.Success)
        {
            if (_config.LogResponseContent)
            {
                _logger?.LogDebug(
                    "Pipeline {PipelineId}: Agent '{Agent}' responded: {Response}",
                    pipelineId, agent, TruncateForLog(response.Content));
            }
            else
            {
                _logger?.LogDebug(
                    "Pipeline {PipelineId}: Agent '{Agent}' responded with {ResponseLength} chars in {Duration}ms",
                    pipelineId, agent, response.Content.Length, response.Duration.TotalMilliseconds);
            }
        }
        else
        {
            _logger?.LogWarning(
                "Pipeline {PipelineId}: Agent '{Agent}' failed: {Error}",
                pipelineId, agent, response.ErrorMessage);
        }
    }

    public void LogRetry(Guid pipelineId, int attemptNumber, IEnumerable<string> errors)
    {
        var errorList = errors.ToList();
        var evt = PipelineEvent.RetryAttempted(pipelineId, attemptNumber, errorList);
        RecordEvent(evt);

        _logger?.LogInformation(
            "Pipeline {PipelineId}: Retry attempt #{AttemptNumber} due to: {Errors}",
            pipelineId, attemptNumber, string.Join("; ", errorList.Take(3)));
    }

    public void LogValidation(Guid pipelineId, ValidationResult result)
    {
        var evt = PipelineEvent.ValidationPerformed(
            pipelineId,
            result.IsValid,
            result.Errors.Count,
            result.Warnings.Count);
        RecordEvent(evt);

        if (result.IsValid)
        {
            _logger?.LogDebug(
                "Pipeline {PipelineId}: Validation passed with {WarningCount} warning(s)",
                pipelineId, result.Warnings.Count);
        }
        else
        {
            _logger?.LogWarning(
                "Pipeline {PipelineId}: Validation failed with {ErrorCount} error(s): {Errors}",
                pipelineId,
                result.Errors.Count,
                string.Join("; ", result.ErrorMessages.Take(3)));
        }
    }

    public void LogDebug(Guid pipelineId, string message, IReadOnlyDictionary<string, object>? data = null)
    {
        var evt = PipelineEvent.Create(
            pipelineId,
            PipelineEventType.Debug,
            message,
            data: data);
        RecordEvent(evt);

        _logger?.LogDebug("Pipeline {PipelineId}: {Message}", pipelineId, message);
    }

    public void LogWarning(Guid pipelineId, string message, IReadOnlyDictionary<string, object>? data = null)
    {
        var evt = PipelineEvent.Create(
            pipelineId,
            PipelineEventType.Warning,
            message,
            data: data);
        RecordEvent(evt);

        _logger?.LogWarning("Pipeline {PipelineId}: {Message}", pipelineId, message);
    }

    public IReadOnlyList<PipelineEvent> GetPipelineHistory(Guid pipelineId)
    {
        lock (_lock)
        {
            return _events
                .Where(e => e.PipelineId == pipelineId)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    public IReadOnlyList<PipelineEvent> GetAllEvents()
    {
        lock (_lock)
        {
            return _events.OrderBy(e => e.Timestamp).ToList();
        }
    }

    public IReadOnlyList<PipelineEvent> GetEventsByType(PipelineEventType type)
    {
        lock (_lock)
        {
            return _events
                .Where(e => e.Type == type)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }

    private void RecordEvent(PipelineEvent evt)
    {
        lock (_lock)
        {
            // Respecter la limite max d'événements
            if (_config.MaxEventsToRetain > 0 && _events.Count >= _config.MaxEventsToRetain)
            {
                // Supprimer les événements les plus anciens
                var toRemove = _events.Count - _config.MaxEventsToRetain + 1;
                _events.RemoveRange(0, toRemove);
            }

            _events.Add(evt);
        }
    }

    private string TruncateForLog(string content)
    {
        if (content.Length <= _config.MaxContentLengthInLogs)
        {
            return content;
        }

        return content[.._config.MaxContentLengthInLogs] + "... [truncated]";
    }

    /// <summary>
    /// Nombre total d'événements enregistrés.
    /// </summary>
    public int EventCount
    {
        get
        {
            lock (_lock)
            {
                return _events.Count;
            }
        }
    }

    /// <summary>
    /// Génère un rapport texte pour un pipeline.
    /// </summary>
    public string GenerateReport(Guid pipelineId)
    {
        var history = GetPipelineHistory(pipelineId);
        if (history.Count == 0)
        {
            return $"No events found for pipeline {pipelineId}";
        }

        var lines = new List<string>
        {
            $"Pipeline Report: {pipelineId}",
            new string('=', 50),
            ""
        };

        foreach (var evt in history)
        {
            var durationStr = evt.Duration.HasValue
                ? $" ({evt.Duration.Value.TotalMilliseconds:F0}ms)"
                : "";

            lines.Add($"[{evt.Timestamp:HH:mm:ss.fff}] {evt.Type}: {evt.Description}{durationStr}");
        }

        // Ajouter un résumé
        var startEvent = history.FirstOrDefault(e => e.Type == PipelineEventType.PipelineStarted);
        var endEvent = history.LastOrDefault(e =>
            e.Type is PipelineEventType.PipelineCompleted or PipelineEventType.PipelineError);

        if (startEvent != null && endEvent != null)
        {
            var totalDuration = endEvent.Timestamp - startEvent.Timestamp;
            var status = endEvent.Type == PipelineEventType.PipelineCompleted ? "SUCCESS" : "FAILED";

            lines.Add("");
            lines.Add(new string('-', 50));
            lines.Add($"Status: {status}");
            lines.Add($"Total Duration: {totalDuration.TotalMilliseconds:F0}ms");
            lines.Add($"Events: {history.Count}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Configuration du logger de pipeline.
/// </summary>
public sealed record PipelineLoggerConfig
{
    /// <summary>
    /// Nombre maximum d'événements à conserver en mémoire.
    /// 0 signifie pas de limite.
    /// </summary>
    public int MaxEventsToRetain { get; init; } = 10000;

    /// <summary>
    /// Longueur maximale du contenu à inclure dans les logs.
    /// </summary>
    public int MaxContentLengthInLogs { get; init; } = 500;

    /// <summary>
    /// Inclure le contenu des prompts dans les logs.
    /// </summary>
    public bool LogPromptContent { get; init; } = false;

    /// <summary>
    /// Inclure le contenu des réponses dans les logs.
    /// </summary>
    public bool LogResponseContent { get; init; } = false;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static PipelineLoggerConfig Default => new();

    /// <summary>
    /// Configuration détaillée pour le débogage.
    /// </summary>
    public static PipelineLoggerConfig Verbose => new()
    {
        LogPromptContent = true,
        LogResponseContent = true,
        MaxContentLengthInLogs = 2000
    };

    /// <summary>
    /// Configuration minimale pour la production.
    /// </summary>
    public static PipelineLoggerConfig Minimal => new()
    {
        MaxEventsToRetain = 1000,
        MaxContentLengthInLogs = 100,
        LogPromptContent = false,
        LogResponseContent = false
    };
}

/// <summary>
/// Logger de pipeline nul (ne fait rien).
/// Utile pour les tests ou quand le logging n'est pas nécessaire.
/// </summary>
public sealed class NullPipelineLogger : IPipelineLogger
{
    public static readonly NullPipelineLogger Instance = new();

    private NullPipelineLogger() { }

    public void LogPipelineStart(Guid pipelineId, NarrativeIntent intent) { }
    public void LogPipelineComplete(Guid pipelineId, TimeSpan duration) { }
    public void LogPipelineError(Guid pipelineId, Exception exception) { }
    public void LogStageStart(Guid pipelineId, string stageName) { }
    public void LogStageComplete(Guid pipelineId, string stageName, TimeSpan duration) { }
    public void LogStageFailure(Guid pipelineId, string stageName, string error) { }
    public void LogAgentCall(Guid pipelineId, AgentType agent, string prompt) { }
    public void LogAgentResponse(Guid pipelineId, AgentType agent, AgentResponse response) { }
    public void LogRetry(Guid pipelineId, int attemptNumber, IEnumerable<string> errors) { }
    public void LogValidation(Guid pipelineId, ValidationResult result) { }
    public void LogDebug(Guid pipelineId, string message, IReadOnlyDictionary<string, object>? data = null) { }
    public void LogWarning(Guid pipelineId, string message, IReadOnlyDictionary<string, object>? data = null) { }

    public IReadOnlyList<PipelineEvent> GetPipelineHistory(Guid pipelineId) => Array.Empty<PipelineEvent>();
    public IReadOnlyList<PipelineEvent> GetAllEvents() => Array.Empty<PipelineEvent>();
    public IReadOnlyList<PipelineEvent> GetEventsByType(PipelineEventType type) => Array.Empty<PipelineEvent>();
    public void Clear() { }
}

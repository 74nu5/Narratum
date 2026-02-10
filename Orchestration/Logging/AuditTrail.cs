using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Logging;

/// <summary>
/// Sévérité d'une entrée d'audit.
/// </summary>
public enum AuditSeverity
{
    /// <summary>
    /// Information de debug.
    /// </summary>
    Debug,

    /// <summary>
    /// Information standard.
    /// </summary>
    Info,

    /// <summary>
    /// Avertissement.
    /// </summary>
    Warning,

    /// <summary>
    /// Erreur.
    /// </summary>
    Error,

    /// <summary>
    /// Erreur critique.
    /// </summary>
    Critical
}

/// <summary>
/// Catégorie d'action auditée.
/// </summary>
public enum AuditCategory
{
    /// <summary>
    /// Actions liées au pipeline.
    /// </summary>
    Pipeline,

    /// <summary>
    /// Actions liées aux agents.
    /// </summary>
    Agent,

    /// <summary>
    /// Actions liées à la validation.
    /// </summary>
    Validation,

    /// <summary>
    /// Actions liées à l'état.
    /// </summary>
    State,

    /// <summary>
    /// Actions liées à la mémoire.
    /// </summary>
    Memory,

    /// <summary>
    /// Actions de sécurité ou d'accès.
    /// </summary>
    Security,

    /// <summary>
    /// Actions système.
    /// </summary>
    System
}

/// <summary>
/// Entrée dans le journal d'audit.
/// </summary>
public sealed record AuditEntry(
    Guid Id,
    Guid PipelineId,
    DateTime Timestamp,
    string Action,
    string Actor,
    string Description,
    AuditSeverity Severity,
    AuditCategory Category,
    IReadOnlyDictionary<string, object>? Details = null)
{
    /// <summary>
    /// Crée une nouvelle entrée d'audit.
    /// </summary>
    public static AuditEntry Create(
        Guid pipelineId,
        string action,
        string actor,
        string description,
        AuditSeverity severity = AuditSeverity.Info,
        AuditCategory category = AuditCategory.Pipeline,
        IReadOnlyDictionary<string, object>? details = null)
    {
        return new AuditEntry(
            Guid.NewGuid(),
            pipelineId,
            DateTime.UtcNow,
            action,
            actor,
            description,
            severity,
            category,
            details);
    }

    /// <summary>
    /// Crée une entrée pour une décision d'orchestration.
    /// </summary>
    public static AuditEntry Decision(
        Guid pipelineId,
        string decision,
        string reason,
        IReadOnlyDictionary<string, object>? context = null)
    {
        return Create(
            pipelineId,
            "Decision",
            "Orchestrator",
            $"{decision}: {reason}",
            AuditSeverity.Info,
            AuditCategory.Pipeline,
            context);
    }

    /// <summary>
    /// Crée une entrée pour une action d'agent.
    /// </summary>
    public static AuditEntry AgentAction(
        Guid pipelineId,
        AgentType agent,
        string action,
        string description)
    {
        return Create(
            pipelineId,
            action,
            agent.ToString(),
            description,
            AuditSeverity.Info,
            AuditCategory.Agent,
            new Dictionary<string, object> { ["agent_type"] = agent.ToString() });
    }

    /// <summary>
    /// Crée une entrée pour un échec de validation.
    /// </summary>
    public static AuditEntry ValidationFailure(
        Guid pipelineId,
        string validationType,
        IEnumerable<string> errors)
    {
        return Create(
            pipelineId,
            "ValidationFailed",
            validationType,
            $"Validation failed: {string.Join("; ", errors.Take(3))}",
            AuditSeverity.Warning,
            AuditCategory.Validation,
            new Dictionary<string, object> { ["errors"] = errors.ToList() });
    }

    /// <summary>
    /// Crée une entrée pour un changement d'état.
    /// </summary>
    public static AuditEntry StateChange(
        Guid pipelineId,
        StateChangeType changeType,
        string description,
        object? oldValue = null,
        object? newValue = null)
    {
        var details = new Dictionary<string, object>
        {
            ["change_type"] = changeType.ToString()
        };

        if (oldValue != null) details["old_value"] = oldValue;
        if (newValue != null) details["new_value"] = newValue;

        return Create(
            pipelineId,
            "StateChange",
            "StateIntegrator",
            description,
            AuditSeverity.Info,
            AuditCategory.State,
            details);
    }

    /// <summary>
    /// Crée une entrée pour une erreur critique.
    /// </summary>
    public static AuditEntry CriticalError(
        Guid pipelineId,
        string source,
        Exception exception)
    {
        return Create(
            pipelineId,
            "CriticalError",
            source,
            exception.Message,
            AuditSeverity.Critical,
            AuditCategory.System,
            new Dictionary<string, object>
            {
                ["exception_type"] = exception.GetType().Name,
                ["stack_trace"] = exception.StackTrace ?? string.Empty
            });
    }
}

/// <summary>
/// Journal d'audit pour tracer les décisions et actions du système.
///
/// Contrairement au PipelineLogger qui trace les événements techniques,
/// l'AuditTrail se concentre sur les décisions métier et les actions
/// qui modifient l'état du système.
/// </summary>
public sealed class AuditTrail
{
    private readonly List<AuditEntry> _entries = new();
    private readonly object _lock = new();
    private readonly AuditTrailConfig _config;

    public AuditTrail(AuditTrailConfig? config = null)
    {
        _config = config ?? AuditTrailConfig.Default;
    }

    /// <summary>
    /// Enregistre une entrée d'audit.
    /// </summary>
    public void Record(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            // Respecter la limite max d'entrées
            if (_config.MaxEntriesToRetain > 0 && _entries.Count >= _config.MaxEntriesToRetain)
            {
                var toRemove = _entries.Count - _config.MaxEntriesToRetain + 1;
                _entries.RemoveRange(0, toRemove);
            }

            _entries.Add(entry);
        }
    }

    /// <summary>
    /// Enregistre une décision d'orchestration.
    /// </summary>
    public void RecordDecision(Guid pipelineId, string decision, string reason)
    {
        Record(AuditEntry.Decision(pipelineId, decision, reason));
    }

    /// <summary>
    /// Enregistre une action d'agent.
    /// </summary>
    public void RecordAgentAction(Guid pipelineId, AgentType agent, string action, string description)
    {
        Record(AuditEntry.AgentAction(pipelineId, agent, action, description));
    }

    /// <summary>
    /// Enregistre un échec de validation.
    /// </summary>
    public void RecordValidationFailure(Guid pipelineId, string validationType, IEnumerable<string> errors)
    {
        Record(AuditEntry.ValidationFailure(pipelineId, validationType, errors));
    }

    /// <summary>
    /// Enregistre un changement d'état.
    /// </summary>
    public void RecordStateChange(
        Guid pipelineId,
        StateChangeType changeType,
        string description,
        object? oldValue = null,
        object? newValue = null)
    {
        Record(AuditEntry.StateChange(pipelineId, changeType, description, oldValue, newValue));
    }

    /// <summary>
    /// Récupère toutes les entrées.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntries(Guid? pipelineId = null)
    {
        lock (_lock)
        {
            var query = _entries.AsEnumerable();

            if (pipelineId.HasValue)
            {
                query = query.Where(e => e.PipelineId == pipelineId.Value);
            }

            return query.OrderBy(e => e.Timestamp).ToList();
        }
    }

    /// <summary>
    /// Récupère les entrées par sévérité.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntriesBySeverity(AuditSeverity severity)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Severity == severity)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    /// <summary>
    /// Récupère les entrées par catégorie.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntriesByCategory(AuditCategory category)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Category == category)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    /// <summary>
    /// Récupère les entrées par action.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntriesByAction(string action)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Action.Equals(action, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    /// <summary>
    /// Récupère les entrées dans une plage de temps.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntriesInRange(DateTime from, DateTime to)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    /// <summary>
    /// Récupère les erreurs et avertissements.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetProblems()
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Severity >= AuditSeverity.Warning)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    /// <summary>
    /// Efface toutes les entrées.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// Nombre total d'entrées.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Génère un rapport d'audit pour un pipeline.
    /// </summary>
    public AuditReport GenerateReport(Guid pipelineId)
    {
        var entries = GetEntries(pipelineId);
        return new AuditReport(pipelineId, entries);
    }

    /// <summary>
    /// Génère un rapport d'audit global.
    /// </summary>
    public AuditReport GenerateGlobalReport()
    {
        var entries = GetEntries();
        return new AuditReport(Guid.Empty, entries);
    }
}

/// <summary>
/// Rapport d'audit généré.
/// </summary>
public sealed record AuditReport(
    Guid PipelineId,
    IReadOnlyList<AuditEntry> Entries)
{
    /// <summary>
    /// Nombre d'entrées dans le rapport.
    /// </summary>
    public int EntryCount => Entries.Count;

    /// <summary>
    /// Nombre d'erreurs critiques.
    /// </summary>
    public int CriticalCount => Entries.Count(e => e.Severity == AuditSeverity.Critical);

    /// <summary>
    /// Nombre d'erreurs.
    /// </summary>
    public int ErrorCount => Entries.Count(e => e.Severity == AuditSeverity.Error);

    /// <summary>
    /// Nombre d'avertissements.
    /// </summary>
    public int WarningCount => Entries.Count(e => e.Severity == AuditSeverity.Warning);

    /// <summary>
    /// Le pipeline a-t-il des problèmes ?
    /// </summary>
    public bool HasProblems => CriticalCount > 0 || ErrorCount > 0;

    /// <summary>
    /// Entrées groupées par catégorie.
    /// </summary>
    public IReadOnlyDictionary<AuditCategory, IReadOnlyList<AuditEntry>> ByCategory =>
        Entries.GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AuditEntry>)g.ToList());

    /// <summary>
    /// Entrées groupées par acteur.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<AuditEntry>> ByActor =>
        Entries.GroupBy(e => e.Actor)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AuditEntry>)g.ToList());

    /// <summary>
    /// Génère une représentation textuelle du rapport.
    /// </summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            PipelineId == Guid.Empty
                ? "Global Audit Report"
                : $"Audit Report for Pipeline: {PipelineId}",
            new string('=', 60),
            "",
            $"Total Entries: {EntryCount}",
            $"Critical: {CriticalCount} | Errors: {ErrorCount} | Warnings: {WarningCount}",
            ""
        };

        if (Entries.Count > 0)
        {
            lines.Add("Entries:");
            lines.Add(new string('-', 60));

            foreach (var entry in Entries)
            {
                var severityMark = entry.Severity switch
                {
                    AuditSeverity.Critical => "[CRIT]",
                    AuditSeverity.Error => "[ERR] ",
                    AuditSeverity.Warning => "[WARN]",
                    AuditSeverity.Info => "[INFO]",
                    AuditSeverity.Debug => "[DBG] ",
                    _ => "[???] "
                };

                lines.Add($"{entry.Timestamp:HH:mm:ss} {severityMark} [{entry.Actor}] {entry.Action}: {entry.Description}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Configuration du journal d'audit.
/// </summary>
public sealed record AuditTrailConfig
{
    /// <summary>
    /// Nombre maximum d'entrées à conserver.
    /// 0 signifie pas de limite.
    /// </summary>
    public int MaxEntriesToRetain { get; init; } = 50000;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static AuditTrailConfig Default => new();

    /// <summary>
    /// Configuration pour les tests (limite basse).
    /// </summary>
    public static AuditTrailConfig ForTesting => new()
    {
        MaxEntriesToRetain = 1000
    };
}

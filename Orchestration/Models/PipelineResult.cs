using Narratum.Core;
using Narratum.Memory;

namespace Narratum.Orchestration.Models;

/// <summary>
/// Statut d'exécution d'une étape du pipeline.
/// </summary>
public enum PipelineStageStatus
{
    /// <summary>
    /// Étape non encore exécutée.
    /// </summary>
    Pending,

    /// <summary>
    /// Étape en cours d'exécution.
    /// </summary>
    Running,

    /// <summary>
    /// Étape terminée avec succès.
    /// </summary>
    Completed,

    /// <summary>
    /// Étape échouée.
    /// </summary>
    Failed,

    /// <summary>
    /// Étape ignorée.
    /// </summary>
    Skipped
}

/// <summary>
/// Résultat de l'exécution d'une étape du pipeline.
/// </summary>
public sealed record PipelineStageResult
{
    /// <summary>
    /// Nom de l'étape.
    /// </summary>
    public string StageName { get; }

    /// <summary>
    /// Statut de l'étape.
    /// </summary>
    public PipelineStageStatus Status { get; }

    /// <summary>
    /// Durée d'exécution.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Message d'erreur si échec.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Données produites par l'étape.
    /// </summary>
    public IReadOnlyDictionary<string, object> OutputData { get; }

    public PipelineStageResult(
        string stageName,
        PipelineStageStatus status,
        TimeSpan duration,
        string? errorMessage = null,
        IReadOnlyDictionary<string, object>? outputData = null)
    {
        StageName = stageName ?? throw new ArgumentNullException(nameof(stageName));
        Status = status;
        Duration = duration;
        ErrorMessage = errorMessage;
        OutputData = outputData ?? new Dictionary<string, object>();
    }

    public static PipelineStageResult Success(string stageName, TimeSpan duration, IReadOnlyDictionary<string, object>? data = null)
        => new(stageName, PipelineStageStatus.Completed, duration, outputData: data);

    public static PipelineStageResult Failure(string stageName, TimeSpan duration, string error)
        => new(stageName, PipelineStageStatus.Failed, duration, error);

    public static PipelineStageResult Skipped(string stageName)
        => new(stageName, PipelineStageStatus.Skipped, TimeSpan.Zero);
}

/// <summary>
/// Sortie narrative produite par le pipeline.
/// </summary>
public sealed record NarrativeOutput
{
    /// <summary>
    /// Identifiant unique de cette sortie.
    /// </summary>
    public Id OutputId { get; }

    /// <summary>
    /// Texte narratif généré.
    /// </summary>
    public string NarrativeText { get; }

    /// <summary>
    /// Memorandum généré à partir de cette sortie.
    /// </summary>
    public Memorandum? GeneratedMemorandum { get; }

    /// <summary>
    /// Événements extraits du texte narratif.
    /// </summary>
    public IReadOnlyList<object> ExtractedEvents { get; }

    /// <summary>
    /// Métadonnées de génération.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Timestamp de génération.
    /// </summary>
    public DateTime GeneratedAt { get; }

    public NarrativeOutput(
        string narrativeText,
        Memorandum? generatedMemorandum = null,
        IEnumerable<object>? extractedEvents = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        OutputId = Id.New();
        NarrativeText = narrativeText ?? throw new ArgumentNullException(nameof(narrativeText));
        GeneratedMemorandum = generatedMemorandum;
        ExtractedEvents = (extractedEvents?.ToList() ?? new List<object>()).AsReadOnly();
        Metadata = metadata ?? new Dictionary<string, object>();
        GeneratedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Crée une sortie minimale (pour les mocks).
    /// </summary>
    public static NarrativeOutput CreateMock(string text)
        => new(text, metadata: new Dictionary<string, object> { ["mock"] = true });
}

/// <summary>
/// Résultat complet de l'exécution du pipeline.
/// </summary>
public sealed record PipelineResult
{
    /// <summary>
    /// Identifiant unique de cette exécution.
    /// </summary>
    public Id ExecutionId { get; }

    /// <summary>
    /// Contexte d'entrée utilisé.
    /// </summary>
    public PipelineContext InputContext { get; }

    /// <summary>
    /// Sortie narrative produite (si succès).
    /// </summary>
    public NarrativeOutput? Output { get; }

    /// <summary>
    /// Indique si l'exécution a réussi.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Message d'erreur global (si échec).
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Résultats de chaque étape.
    /// </summary>
    public IReadOnlyList<PipelineStageResult> StageResults { get; }

    /// <summary>
    /// Durée totale d'exécution.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Nombre de tentatives (retries).
    /// </summary>
    public int RetryCount { get; }

    /// <summary>
    /// Timestamp de début d'exécution.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Timestamp de fin d'exécution.
    /// </summary>
    public DateTime CompletedAt { get; }

    private PipelineResult(
        PipelineContext inputContext,
        NarrativeOutput? output,
        bool isSuccess,
        string? errorMessage,
        IEnumerable<PipelineStageResult> stageResults,
        TimeSpan totalDuration,
        int retryCount,
        DateTime startedAt,
        DateTime completedAt)
    {
        ExecutionId = Id.New();
        InputContext = inputContext ?? throw new ArgumentNullException(nameof(inputContext));
        Output = output;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StageResults = stageResults.ToList().AsReadOnly();
        TotalDuration = totalDuration;
        RetryCount = retryCount;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Crée un résultat de succès.
    /// </summary>
    public static PipelineResult Success(
        PipelineContext context,
        NarrativeOutput output,
        IEnumerable<PipelineStageResult> stageResults,
        TimeSpan duration,
        int retryCount = 0)
    {
        var now = DateTime.UtcNow;
        return new PipelineResult(
            context,
            output,
            isSuccess: true,
            errorMessage: null,
            stageResults,
            duration,
            retryCount,
            startedAt: now.Subtract(duration),
            completedAt: now);
    }

    /// <summary>
    /// Crée un résultat d'échec.
    /// </summary>
    public static PipelineResult Failure(
        PipelineContext context,
        string errorMessage,
        IEnumerable<PipelineStageResult> stageResults,
        TimeSpan duration,
        int retryCount = 0)
    {
        var now = DateTime.UtcNow;
        return new PipelineResult(
            context,
            output: null,
            isSuccess: false,
            errorMessage,
            stageResults,
            duration,
            retryCount,
            startedAt: now.Subtract(duration),
            completedAt: now);
    }
}

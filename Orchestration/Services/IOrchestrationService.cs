using Narratum.Core;
using Narratum.State;
using Narratum.Orchestration.Models;

namespace Narratum.Orchestration.Services;

/// <summary>
/// Configuration de l'orchestration.
/// </summary>
public sealed record OrchestrationConfig
{
    /// <summary>
    /// Nombre maximum de tentatives en cas d'échec.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Timeout pour chaque étape du pipeline.
    /// </summary>
    public TimeSpan StageTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout global pour l'exécution complète.
    /// </summary>
    public TimeSpan GlobalTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Active le logging détaillé.
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;

    /// <summary>
    /// Utilise les agents mock.
    /// </summary>
    public bool UseMockAgents { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static OrchestrationConfig Default => new();

    /// <summary>
    /// Configuration pour les tests.
    /// </summary>
    public static OrchestrationConfig ForTesting => new()
    {
        MaxRetries = 1,
        StageTimeout = TimeSpan.FromSeconds(5),
        GlobalTimeout = TimeSpan.FromSeconds(30),
        EnableDetailedLogging = true,
        UseMockAgents = true
    };
}

/// <summary>
/// Service principal d'orchestration narrative.
///
/// L'orchestrateur coordonne l'exécution du pipeline de génération
/// narrative, gérant les agents, la validation, et les retries.
///
/// Responsabilités :
/// - Construire le contexte narratif
/// - Exécuter le pipeline de génération
/// - Valider les sorties
/// - Gérer les erreurs et retries
/// - Intégrer les résultats dans l'état
/// </summary>
public interface IOrchestrationService
{
    /// <summary>
    /// Exécute un cycle complet de génération narrative.
    /// </summary>
    /// <param name="storyState">État actuel de l'histoire.</param>
    /// <param name="intent">Intention narrative à réaliser.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résultat du pipeline contenant la sortie narrative.</returns>
    Task<Result<PipelineResult>> ExecuteCycleAsync(
        StoryState storyState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Construit le contexte narratif à partir de l'état et de l'intention.
    /// </summary>
    /// <param name="storyState">État actuel.</param>
    /// <param name="intent">Intention narrative.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Contexte enrichi pour le pipeline.</returns>
    Task<Result<PipelineContext>> BuildContextAsync(
        StoryState storyState,
        NarrativeIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide une sortie narrative contre le contexte.
    /// </summary>
    /// <param name="output">Sortie à valider.</param>
    /// <param name="context">Contexte de validation.</param>
    /// <returns>Succès si valide, erreur avec détails sinon.</returns>
    Result<Unit> ValidateOutput(NarrativeOutput output, PipelineContext context);

    /// <summary>
    /// Configuration actuelle de l'orchestrateur.
    /// </summary>
    OrchestrationConfig Config { get; }

    /// <summary>
    /// Indique si l'orchestrateur est prêt.
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}

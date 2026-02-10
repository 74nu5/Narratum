using Narratum.Core;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Interface pour exécuter les agents de génération.
///
/// L'AgentExecutor est responsable de :
/// - Exécuter les prompts sur les agents appropriés
/// - Gérer l'ordre d'exécution (séquentiel/parallèle)
/// - Collecter les réponses des agents
/// - Gérer les erreurs d'exécution
/// </summary>
public interface IAgentExecutor
{
    /// <summary>
    /// Exécute les prompts sur les agents.
    /// </summary>
    /// <param name="prompts">Ensemble de prompts à exécuter.</param>
    /// <param name="context">Contexte narratif.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Sortie brute des agents.</returns>
    Task<Result<RawOutput>> ExecuteAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Réexécute les agents pour corriger une sortie invalide.
    /// </summary>
    /// <param name="previousOutput">Sortie précédente invalide.</param>
    /// <param name="validationResult">Résultat de validation avec erreurs.</param>
    /// <param name="context">Contexte narratif.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Nouvelle sortie corrigée.</returns>
    Task<Result<RawOutput>> RewriteAsync(
        RawOutput previousOutput,
        ValidationResult validationResult,
        NarrativeContext context,
        CancellationToken cancellationToken = default);
}

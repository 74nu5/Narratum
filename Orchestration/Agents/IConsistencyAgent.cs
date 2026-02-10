using Narratum.Core;
using Narratum.Memory;

namespace Narratum.Orchestration.Agents;

/// <summary>
/// Agent spécialisé dans la vérification de cohérence narrative.
///
/// Responsabilités :
/// - Vérifier la cohérence avec l'état canonique
/// - Détecter les contradictions dans le texte généré
/// - Suggérer des corrections
/// </summary>
public interface IConsistencyAgent : IAgent
{
    /// <summary>
    /// Vérifie la cohérence d'un texte généré.
    /// </summary>
    /// <param name="generatedText">Texte à vérifier.</param>
    /// <param name="canonicalState">État canonique de référence.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résultat de la vérification.</returns>
    Task<Result<ConsistencyCheck>> CheckConsistencyAsync(
        string generatedText,
        CanonicalState canonicalState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggère des corrections pour des violations détectées.
    /// </summary>
    /// <param name="text">Texte original.</param>
    /// <param name="violations">Violations de cohérence détectées.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Texte corrigé ou suggestions.</returns>
    Task<Result<string>> SuggestCorrectionsAsync(
        string text,
        IReadOnlyList<CoherenceViolation> violations,
        CancellationToken cancellationToken = default);
}

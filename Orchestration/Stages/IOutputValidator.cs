using Narratum.Core;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Interface pour valider les sorties des agents.
///
/// L'OutputValidator est responsable de :
/// - Vérifier la structure des sorties
/// - Valider la cohérence avec l'état du monde
/// - Détecter les contradictions logiques
/// - Identifier les erreurs à corriger
/// </summary>
public interface IOutputValidator
{
    /// <summary>
    /// Valide la sortie brute des agents.
    /// </summary>
    /// <param name="output">Sortie brute à valider.</param>
    /// <param name="context">Contexte narratif.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résultat de validation.</returns>
    Task<ValidationResult> ValidateAsync(
        RawOutput output,
        NarrativeContext context,
        CancellationToken cancellationToken = default);
}

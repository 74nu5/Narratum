namespace Narratum.Memory.Services;

/// <summary>
/// Interface pour valider la cohérence logique de l'état narratif.
/// Détecte les contradictions, violations de séquence, et incohérences d'entités.
/// </summary>
public interface ICoherenceValidator
{
    /// <summary>
    /// Valide l'état canonique pour détecter les contradictions internes.
    /// </summary>
    IReadOnlyList<CoherenceViolation> ValidateState(CanonicalState state);

    /// <summary>
    /// Valide une transition d'état pour détecter les changements logiquement impossibles.
    /// </summary>
    IReadOnlyList<CoherenceViolation> ValidateTransition(
        CanonicalState previousState,
        CanonicalState newState);

    /// <summary>
    /// Détecte si deux faits se contredisent logiquement.
    /// </summary>
    bool ContainsContradiction(Fact fact1, Fact fact2);

    /// <summary>
    /// Valide un fait isolé pour détecter les problèmes internes.
    /// </summary>
    CoherenceViolation? ValidateFact(Fact fact);

    /// <summary>
    /// Valide un ensemble de faits pour détecter les contradictions croisées.
    /// </summary>
    IReadOnlyList<CoherenceViolation> ValidateFacts(IReadOnlyList<Fact> facts);
}

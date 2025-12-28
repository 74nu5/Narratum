namespace Narratum.Memory.Services;

/// <summary>
/// Implémentation complète de la validation de cohérence logique.
/// Supporte:
/// - Détection de contradictions simples (X is Y vs X is not Y)
/// - Violations de séquence temporelle (mort puis survie)
/// - Incohérences d'entités (état impossible)
/// </summary>
public class CoherenceValidator : ICoherenceValidator
{
    private const string DEAD_PATTERN = "dead|died|deceased|death";
    private const string ALIVE_PATTERN = "alive|living|living still";
    private const string DESTROYED_PATTERN = "destroyed|in ruins|leveled";
    private const string INTACT_PATTERN = "intact|standing|safe";

    public IReadOnlyList<CoherenceViolation> ValidateState(CanonicalState state)
    {
        var violations = new List<CoherenceViolation>();

        // Vérifier les contradictions internes entre les faits
        var facts = state.Facts.ToList();
        violations.AddRange(ValidateFacts(facts));

        return violations;
    }

    public IReadOnlyList<CoherenceViolation> ValidateTransition(
        CanonicalState previousState,
        CanonicalState newState)
    {
        var violations = new List<CoherenceViolation>();

        // Extraire les faits des états
        var prevFacts = previousState.Facts.ToList();
        var newFacts = newState.Facts.ToList();

        // Vérifier que les transitions sont logiquement possibles
        foreach (var newFact in newFacts)
        {
            var correspondingPrevFact = prevFacts
                .FirstOrDefault(f => SharesEntity(f, newFact));

            if (correspondingPrevFact != null)
            {
                // Vérifier les transitions impossibles
                if (IsDeadPattern(correspondingPrevFact.Content) && 
                    IsAlivePattern(newFact.Content) &&
                    SameEntity(correspondingPrevFact, newFact))
                {
                    violations.Add(CoherenceViolation.Create(
                        violationType: CoherenceViolationType.SequenceViolation,
                        severity: CoherenceSeverity.Error,
                        description: $"Impossible: résurrection détectée. Ancien: '{correspondingPrevFact.Content}', Nouveau: '{newFact.Content}'",
                        involvedFactIds: new[] { correspondingPrevFact.Id, newFact.Id }
                    ));
                }
            }
        }

        return violations;
    }

    public bool ContainsContradiction(Fact fact1, Fact fact2)
    {
        if (fact1.Id == fact2.Id)
            return false;

        var content1 = fact1.Content.ToLowerInvariant();
        var content2 = fact2.Content.ToLowerInvariant();

        // Si les entités ne partagent rien, pas de contradiction
        if (!SharesEntity(fact1, fact2))
            return false;

        // Vérifier les patterns opposés
        var isDeadVsAlive = (IsDeadPattern(content1) && IsAlivePattern(content2)) ||
                            (IsAlivePattern(content1) && IsDeadPattern(content2));
        
        var isDestroyedVsIntact = (IsDestroyedPattern(content1) && IsIntactPattern(content2)) ||
                                  (IsIntactPattern(content1) && IsDestroyedPattern(content2));

        return isDeadVsAlive || isDestroyedVsIntact || ContainsOppositeAssertion(content1, content2);
    }

    public CoherenceViolation? ValidateFact(Fact fact)
    {
        // Vérifier les propriétés basiques
        if (string.IsNullOrWhiteSpace(fact.Content))
        {
            return CoherenceViolation.Create(
                violationType: CoherenceViolationType.StatementContradiction,
                severity: CoherenceSeverity.Error,
                description: "Le contenu du fait ne peut pas être vide",
                involvedFactIds: new[] { fact.Id }
            );
        }

        // Vérifier la confiance
        if (fact.Confidence < 0 || fact.Confidence > 1)
        {
            return CoherenceViolation.Create(
                violationType: CoherenceViolationType.StatementContradiction,
                severity: CoherenceSeverity.Error,
                description: $"Score de confiance doit être entre 0 et 1, trouvé: {fact.Confidence}",
                involvedFactIds: new[] { fact.Id }
            );
        }

        return null;
    }

    public IReadOnlyList<CoherenceViolation> ValidateFacts(IReadOnlyList<Fact> facts)
    {
        var violations = new List<CoherenceViolation>();

        // Valider chaque fait individuellement
        foreach (var fact in facts)
        {
            var violation = ValidateFact(fact);
            if (violation != null)
                violations.Add(violation);
        }

        // Vérifier les contradictions entre faits
        for (int i = 0; i < facts.Count; i++)
        {
            for (int j = i + 1; j < facts.Count; j++)
            {
                if (ContainsContradiction(facts[i], facts[j]))
                {
                    violations.Add(CoherenceViolation.Create(
                        violationType: CoherenceViolationType.StatementContradiction,
                        severity: CoherenceSeverity.Error,
                        description: $"Contradiction: '{facts[i].Content}' vs '{facts[j].Content}'",
                        involvedFactIds: new[] { facts[i].Id, facts[j].Id }
                    ));
                }
            }
        }

        return violations;
    }

    private bool SharesEntity(Fact fact1, Fact fact2)
    {
        return fact1.EntityReferences
            .Intersect(fact2.EntityReferences)
            .Any();
    }

    private bool SameEntity(Fact fact1, Fact fact2)
    {
        return fact1.EntityReferences.SetEquals(fact2.EntityReferences);
    }

    private bool IsDeadPattern(string content)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            content, DEAD_PATTERN, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsAlivePattern(string content)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            content, ALIVE_PATTERN, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsDestroyedPattern(string content)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            content, DESTROYED_PATTERN, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsIntactPattern(string content)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            content, INTACT_PATTERN, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool ContainsOppositeAssertion(string content1, string content2)
    {
        // Chercher "is X" vs "is not X"
        if (content1.Contains(" is ") && content2.Contains(" is "))
        {
            var pred1 = ExtractPredicate(content1);
            var pred2 = ExtractPredicate(content2);

            return (content1.Contains(" is ") && content2.Contains(" is not ") && pred1 == pred2) ||
                   (content1.Contains(" is not ") && content2.Contains(" is ") && pred1 == pred2);
        }

        return false;
    }

    private string ExtractPredicate(string content)
    {
        var parts = content.Split(" is ");
        return parts.Length > 1 ? parts[1].Trim() : "";
    }
}

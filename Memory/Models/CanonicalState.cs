namespace Narratum.Memory;

/// <summary>
/// Record immutable représentant l'État Canonique du monde narratif.
/// C'est l'état "accepted as true" selon la hiérarchie logique du système.
/// Composé de faits qui ont tous été validés comme cohérents entre eux.
/// </summary>
/// <param name="Id">Identifiant unique de l'état canonique</param>
/// <param name="WorldId">Identifiant du monde narratif concerné</param>
/// <param name="Facts">Ensemble des faits canoniques (cohérents)</param>
/// <param name="MemoryLevel">Niveau hiérarchique auquel cet état s'applique</param>
/// <param name="Version">Numéro de version (incrémenté à chaque modification)</param>
/// <param name="LastUpdated">Timestamp de la dernière mise à jour</param>
public sealed record CanonicalState(
    Guid Id,
    Guid WorldId,
    IReadOnlySet<Fact> Facts,
    MemoryLevel MemoryLevel,
    int Version = 1,
    DateTime? LastUpdated = null
)
{
    /// <summary>
    /// Crée un nouvel état canonique vide.
    /// </summary>
    public static CanonicalState CreateEmpty(Guid worldId, MemoryLevel memoryLevel)
    {
        return new CanonicalState(
            Id: Guid.NewGuid(),
            WorldId: worldId,
            Facts: new HashSet<Fact>(),
            MemoryLevel: memoryLevel,
            Version: 1,
            LastUpdated: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Ajoute un fait à l'état canonique et retourne un nouvel état (immutable).
    /// </summary>
    public CanonicalState AddFact(Fact fact)
    {
        if (!fact.Validate())
            throw new ArgumentException("Fact is invalid", nameof(fact));

        var newFacts = new HashSet<Fact>(Facts) { fact };
        return this with
        {
            Facts = newFacts,
            Version = Version + 1,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Ajoute plusieurs faits à l'état canonique et retourne un nouvel état.
    /// </summary>
    public CanonicalState AddFacts(IEnumerable<Fact> facts)
    {
        var validatedFacts = facts.ToList();
        foreach (var fact in validatedFacts)
        {
            if (!fact.Validate())
                throw new ArgumentException("One or more facts are invalid", nameof(facts));
        }

        var newFacts = new HashSet<Fact>(Facts);
        foreach (var fact in validatedFacts)
        {
            newFacts.Add(fact);
        }

        return this with
        {
            Facts = newFacts,
            Version = Version + 1,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Supprime un fait de l'état canonique et retourne un nouvel état.
    /// </summary>
    public CanonicalState RemoveFact(Guid factId)
    {
        var newFacts = new HashSet<Fact>(Facts.Where(f => f.Id != factId));
        return this with
        {
            Facts = newFacts,
            Version = Version + 1,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Récupère tous les faits concernant une entité spécifique.
    /// </summary>
    public IEnumerable<Fact> GetFactsForEntity(string entityName)
    {
        return Facts.Where(f => f.EntityReferences.Contains(entityName));
    }

    /// <summary>
    /// Récupère les faits d'un type spécifique.
    /// </summary>
    public IEnumerable<Fact> GetFactsByType(FactType factType)
    {
        return Facts.Where(f => f.FactType == factType);
    }

    /// <summary>
    /// Valide que l'état canonique ne contient que des faits valides.
    /// </summary>
    public bool Validate()
    {
        return Facts.All(f => f.Validate());
    }

    /// <summary>
    /// Retourne le nombre total de faits dans l'état.
    /// </summary>
    public int FactCount => Facts.Count;
}

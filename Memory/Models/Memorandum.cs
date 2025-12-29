namespace Narratum.Memory;

/// <summary>
/// Record immutable représentant un Mémorandum - un container structuré de faits organisés par niveau hiérarchique.
/// Le Mémorandum est la principale abstraction pour gérer la mémoire narrative du système.
/// </summary>
/// <param name="Id">Identifiant unique du mémorandum</param>
/// <param name="WorldId">Identifiant du monde narratif concerné</param>
/// <param name="Title">Titre du mémorandum</param>
/// <param name="Description">Description du contenu</param>
/// <param name="CanonicalStates">États canoniques par niveau hiérarchique</param>
/// <param name="Violations">Violations de cohérence détectées</param>
/// <param name="CreatedAt">Timestamp de création</param>
/// <param name="LastUpdated">Timestamp de la dernière mise à jour</param>
public sealed record Memorandum(
    Guid Id,
    Guid WorldId,
    string Title,
    string Description,
    IReadOnlyDictionary<MemoryLevel, CanonicalState> CanonicalStates,
    IReadOnlySet<CoherenceViolation> Violations,
    DateTime CreatedAt,
    DateTime LastUpdated
)
{
    /// <summary>
    /// Crée un nouveau Mémorandum vide pour un monde donné.
    /// </summary>
    public static Memorandum CreateEmpty(Guid worldId, string title, string description = "")
    {
        var canonicalStates = new Dictionary<MemoryLevel, CanonicalState>
        {
            { MemoryLevel.Event, CanonicalState.CreateEmpty(worldId, MemoryLevel.Event) },
            { MemoryLevel.Chapter, CanonicalState.CreateEmpty(worldId, MemoryLevel.Chapter) },
            { MemoryLevel.Arc, CanonicalState.CreateEmpty(worldId, MemoryLevel.Arc) },
            { MemoryLevel.World, CanonicalState.CreateEmpty(worldId, MemoryLevel.World) }
        };

        return new Memorandum(
            Id: Guid.NewGuid(),
            WorldId: worldId,
            Title: title,
            Description: description,
            CanonicalStates: canonicalStates,
            Violations: new HashSet<CoherenceViolation>(),
            CreatedAt: DateTime.UtcNow,
            LastUpdated: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Ajoute un fait au niveau hiérarchique spécifié et retourne un nouveau Mémorandum.
    /// </summary>
    public Memorandum AddFact(MemoryLevel level, Fact fact)
    {
        if (!fact.Validate())
            throw new ArgumentException("Fact is invalid", nameof(fact));

        if (!CanonicalStates.TryGetValue(level, out var currentState))
            throw new ArgumentException($"Memory level {level} not found", nameof(level));

        var updatedState = currentState.AddFact(fact);
        var newStates = new Dictionary<MemoryLevel, CanonicalState>(CanonicalStates);
        newStates[level] = updatedState;

        return this with
        {
            CanonicalStates = newStates,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Ajoute plusieurs faits au niveau hiérarchique spécifié et retourne un nouveau Mémorandum.
    /// </summary>
    public Memorandum AddFacts(MemoryLevel level, IEnumerable<Fact> facts)
    {
        var factsList = facts.ToList();
        foreach (var fact in factsList)
        {
            if (!fact.Validate())
                throw new ArgumentException("One or more facts are invalid", nameof(facts));
        }

        if (!CanonicalStates.TryGetValue(level, out var currentState))
            throw new ArgumentException($"Memory level {level} not found", nameof(level));

        var updatedState = currentState.AddFacts(factsList);
        var newStates = new Dictionary<MemoryLevel, CanonicalState>(CanonicalStates);
        newStates[level] = updatedState;

        return this with
        {
            CanonicalStates = newStates,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Enregistre une violation de cohérence et retourne un nouveau Mémorandum.
    /// </summary>
    public Memorandum AddViolation(CoherenceViolation violation)
    {
        if (!violation.Validate())
            throw new ArgumentException("Violation is invalid", nameof(violation));

        var newViolations = new HashSet<CoherenceViolation>(Violations) { violation };
        return this with
        {
            Violations = newViolations,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Enregistre plusieurs violations et retourne un nouveau Mémorandum.
    /// </summary>
    public Memorandum AddViolations(IEnumerable<CoherenceViolation> violations)
    {
        var violationsList = violations.ToList();
        foreach (var violation in violationsList)
        {
            if (!violation.Validate())
                throw new ArgumentException("One or more violations are invalid", nameof(violations));
        }

        var newViolations = new HashSet<CoherenceViolation>(Violations);
        foreach (var violation in violationsList)
        {
            newViolations.Add(violation);
        }

        return this with
        {
            Violations = newViolations,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Résout une violation et retourne un nouveau Mémorandum.
    /// </summary>
    public Memorandum ResolveViolation(Guid violationId)
    {
        var violation = Violations.FirstOrDefault(v => v.Id == violationId);
        if (violation == null)
            throw new ArgumentException("Violation not found", nameof(violationId));

        var newViolations = new HashSet<CoherenceViolation>(Violations.Where(v => v.Id != violationId))
        {
            violation.MarkResolved()
        };

        return this with
        {
            Violations = newViolations,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Récupère l'état canonique à un niveau hiérarchique spécifique.
    /// </summary>
    public CanonicalState GetCanonicalState(MemoryLevel level)
    {
        if (!CanonicalStates.TryGetValue(level, out var state))
            throw new ArgumentException($"Memory level {level} not found", nameof(level));
        return state;
    }

    /// <summary>
    /// Récupère tous les faits à un niveau spécifique.
    /// </summary>
    public IEnumerable<Fact> GetFacts(MemoryLevel level)
    {
        return GetCanonicalState(level).Facts;
    }

    /// <summary>
    /// Récupère les faits concernant une entité à un niveau spécifique.
    /// </summary>
    public IEnumerable<Fact> GetFactsForEntity(MemoryLevel level, string entityName)
    {
        return GetCanonicalState(level).GetFactsForEntity(entityName);
    }

    /// <summary>
    /// Récupère les violations non résolues.
    /// </summary>
    public IEnumerable<CoherenceViolation> GetUnresolvedViolations()
    {
        return Violations.Where(v => !v.IsResolved);
    }

    /// <summary>
    /// Récupère les violations résolues.
    /// </summary>
    public IEnumerable<CoherenceViolation> GetResolvedViolations()
    {
        return Violations.Where(v => v.IsResolved);
    }

    /// <summary>
    /// Récupère les violations d'une gravité spécifique.
    /// </summary>
    public IEnumerable<CoherenceViolation> GetViolationsBySeverity(CoherenceSeverity severity)
    {
        return Violations.Where(v => v.Severity == severity);
    }

    /// <summary>
    /// Valide que le Mémorandum est cohérent.
    /// </summary>
    public bool Validate()
    {
        // Tous les états canoniques doivent être valides
        foreach (var state in CanonicalStates.Values)
        {
            if (!state.Validate())
                return false;
        }

        // Tous les violations doivent être valides
        foreach (var violation in Violations)
        {
            if (!violation.Validate())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Retourne un résumé du contenu du Mémorandum.
    /// </summary>
    public string GetSummary()
    {
        var lines = new List<string>
        {
            $"Mémorandum: {Title}",
            Description,
            $"Créé: {CreatedAt:g}",
            $"Mis à jour: {LastUpdated:g}",
            "",
            "Faits par niveau:"
        };

        foreach (var level in new[] { MemoryLevel.Event, MemoryLevel.Chapter, MemoryLevel.Arc, MemoryLevel.World })
        {
            var count = CanonicalStates[level].FactCount;
            lines.Add($"  {level}: {count} fait(s)");
        }

        var unresolved = GetUnresolvedViolations().Count();
        var resolved = GetResolvedViolations().Count();
        lines.Add("");
        lines.Add($"Violations: {unresolved} non résolues, {resolved} résolues");

        return string.Join(Environment.NewLine, lines);
    }
}

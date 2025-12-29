namespace Narratum.Memory;

/// <summary>
/// Enumération des niveaux hiérarchiques de mémoire.
/// Level 0: Événement unique
/// Level 1: Chapitre (groupe d'événements)
/// Level 2: Arc (groupe de chapitres)
/// Level 3: Monde (histoire complète)
/// </summary>
public enum MemoryLevel
{
    Event = 0,      // Un seul événement
    Chapter = 1,    // Groupe d'événements
    Arc = 2,        // Groupe de chapitres
    World = 3       // Histoire complète
}

/// <summary>
/// Types de faits qui peuvent être extraits d'événements narratifs.
/// </summary>
public enum FactType
{
    CharacterState,      // "Aric is dead"
    LocationState,       // "Tower is destroyed"
    Relationship,        // "Aric trusts Lyra"
    Knowledge,          // "Crystal has power"
    Event,              // "Combat occurred"
    Contradiction       // "Aric is both alive and dead"
}

/// <summary>
/// Types de violations de cohérence logique détectées.
/// </summary>
public enum CoherenceViolationType
{
    StatementContradiction,    // "X is true" vs "X is false"
    SequenceViolation,         // Timeline impossible
    EntityInconsistency,       // Character state mismatch
    LocationInconsistency      // Location state mismatch
}

/// <summary>
/// Gravité d'une violation de cohérence.
/// </summary>
public enum CoherenceSeverity
{
    Info,      // Non problématique
    Warning,   // Potentiellement problématique
    Error      // Brise la cohérence logique
}

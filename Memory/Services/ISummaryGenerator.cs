using Narratum.Memory;

namespace Narratum.Memory.Services;

/// <summary>
/// Interface pour la génération déterministe de résumés narratifs.
/// Organise les faits et résumés en une hiérarchie cohérente:
/// Facts → Chapter → Arc → World
/// </summary>
public interface ISummaryGenerator
{
    /// <summary>
    /// Résume un ensemble de faits au niveau chapitre.
    /// Produit une phrase ou deux qui capture les points clés.
    /// </summary>
    /// <param name="chapterFacts">Les faits du chapitre (triés par timestamp)</param>
    /// <returns>Résumé déterministe du chapitre</returns>
    string SummarizeChapter(IReadOnlyList<Fact> chapterFacts);

    /// <summary>
    /// Résume un ensemble de résumés de chapitres au niveau arc.
    /// Agrège les points clés pour créer un résumé narratif cohérent.
    /// </summary>
    /// <param name="chapterSummaries">Les résumés des chapitres de cet arc</param>
    /// <returns>Résumé déterministe de l'arc</returns>
    string SummarizeArc(IReadOnlyList<string> chapterSummaries);

    /// <summary>
    /// Crée un résumé complet du monde basé sur les arcs narratifs.
    /// Produit une histoire globale structurée.
    /// </summary>
    /// <param name="arcSummaries">Les résumés de tous les arcs</param>
    /// <returns>Résumé complet déterministe du monde</returns>
    string SummarizeWorld(IReadOnlyList<string> arcSummaries);

    /// <summary>
    /// Filtre et ordonne les faits pour un résumé optimal.
    /// Élimine les redondances tout en préservant les détails importants.
    /// </summary>
    /// <param name="facts">Tous les faits disponibles</param>
    /// <param name="maxFacts">Nombre maximal de faits à garder</param>
    /// <returns>Faits filtrés et ordonnés de manière déterministe</returns>
    IReadOnlyList<Fact> FilterImportantFacts(IReadOnlyList<Fact> facts, int maxFacts = 10);

    /// <summary>
    /// Extrait les points clés d'un résumé textuel.
    /// Utilisé pour l'agrégation hiérarchique.
    /// </summary>
    /// <param name="summary">Le résumé source</param>
    /// <returns>Liste déterministe des points clés</returns>
    IReadOnlyList<string> ExtractKeyPoints(string summary);
}

/// <summary>
/// Service principal pour la génération de résumés avec logique pure et déterministe.
/// Implémente une hiérarchie : Facts → Chapters → Arcs → World
/// </summary>
public class SummaryGeneratorService : ISummaryGenerator
{
    /// <summary>
    /// Résume un ensemble de faits au niveau chapitre.
    /// Stratégie: Filtrer → Ordonner → Concaténer
    /// </summary>
    public string SummarizeChapter(IReadOnlyList<Fact> chapterFacts)
    {
        if (chapterFacts.Count == 0)
            return "[No events]";

        // Filtrer les faits importants (haute confiance)
        var importantFacts = FilterImportantFacts(chapterFacts, maxFacts: 5);

        // Ordonner par timestamp pour cohérence narrative
        var orderedFacts = importantFacts
            .OrderBy(f => f.CreatedAt ?? DateTime.MinValue)
            .ToList();

        // Construire le résumé de manière déterministe
        if (orderedFacts.Count == 0)
            return "[No significant events]";

        if (orderedFacts.Count == 1)
            return orderedFacts[0].Content;

        // Plusieurs faits: les concatener avec " | "
        var summaryContent = string.Join(" | ", 
            orderedFacts.Select(f => f.Content));

        // Limiter à 300 caractères pour un chapitre
        return summaryContent.Length > 300 
            ? summaryContent[..297] + "…" 
            : summaryContent;
    }

    /// <summary>
    /// Résume un arc en agrégeant les résumés de chapitres.
    /// Stratégie: Extraire points clés → Filtrer → Ordonner → Synthétiser
    /// </summary>
    public string SummarizeArc(IReadOnlyList<string> chapterSummaries)
    {
        if (chapterSummaries.Count == 0)
            return "[No chapters]";

        // Extraire les points clés de chaque chapitre
        var allKeyPoints = chapterSummaries
            .SelectMany(s => ExtractKeyPoints(s))
            .ToList();

        if (allKeyPoints.Count == 0)
            return string.Join(" → ", chapterSummaries.Take(3));

        // Filtrer les doublons (points identiques)
        var uniquePoints = allKeyPoints
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p) // Tri déterministe alphabétique
            .ToList();

        // Garder les 10 points clés maximum
        var topPoints = uniquePoints.Take(10).ToList();

        // Construire le résumé d'arc
        var arcSummary = string.Join(" → ", topPoints);

        return arcSummary.Length > 500 
            ? arcSummary[..497] + "…" 
            : arcSummary;
    }

    /// <summary>
    /// Crée une synthèse globale du monde.
    /// Stratégie: Structurer les arcs → Ajouter contexte → Formatter
    /// </summary>
    public string SummarizeWorld(IReadOnlyList<string> arcSummaries)
    {
        if (arcSummaries.Count == 0)
            return "[Empty world history]";

        var worldBuilder = new System.Text.StringBuilder();

        // Ajouter une introduction
        worldBuilder.AppendLine($"## World History ({arcSummaries.Count} Arcs)");
        worldBuilder.AppendLine();

        // Ajouter chaque arc avec numérotation déterministe
        for (int i = 0; i < arcSummaries.Count; i++)
        {
            var arcNumber = i + 1;
            worldBuilder.AppendLine($"### Arc {arcNumber}");
            worldBuilder.AppendLine(arcSummaries[i]);
            worldBuilder.AppendLine();
        }

        // Ajouter une synthèse finale
        var majorEvents = arcSummaries
            .SelectMany(s => ExtractKeyPoints(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .Take(5)
            .ToList();

        if (majorEvents.Count > 0)
        {
            worldBuilder.AppendLine("### Major Events");
            foreach (var evt in majorEvents)
            {
                worldBuilder.AppendLine($"- {evt}");
            }
        }

        return worldBuilder.ToString();
    }

    /// <summary>
    /// Filtre et ordonne les faits de manière déterministe.
    /// Priorité: Confiance haute > Changements d'état > Autres
    /// </summary>
    public IReadOnlyList<Fact> FilterImportantFacts(IReadOnlyList<Fact> facts, int maxFacts = 10)
    {
        if (facts.Count == 0)
            return new List<Fact>();

        // Trier par importance:
        // 1. Confiance élevée d'abord (>= 0.8)
        // 2. Ensuite par type (CharacterState > LocationState > Other)
        // 3. Puis par timestamp
        // 4. Enfin par ID (pour déterminisme total)
        var filtered = facts
            .Where(f => !string.IsNullOrWhiteSpace(f.Content))
            .OrderByDescending(f => f.Confidence)
            .ThenByDescending(f => GetFactTypePriority(f.FactType))
            .ThenBy(f => f.CreatedAt ?? DateTime.MinValue)
            .ThenBy(f => f.Id.ToString())
            .Take(maxFacts)
            .ToList();

        return filtered;
    }

    /// <summary>
    /// Extrait les points clés d'un résumé textuel.
    /// Utilise le séparateur " | " ou " → " pour extraire les phrases.
    /// </summary>
    public IReadOnlyList<string> ExtractKeyPoints(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return new List<string>();

        // Chercher d'abord le séparateur " → " (arc level)
        var points = summary.Contains(" → ")
            ? summary.Split(" → ")
            : summary.Split(" | ");

        var cleanedPoints = points
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p) // Tri déterministe
            .ToList();

        return cleanedPoints;
    }

    /// <summary>
    /// Helper pour déterminer la priorité d'un type de fait.
    /// Aide au tri déterministe des faits importants.
    /// </summary>
    private int GetFactTypePriority(FactType factType)
    {
        return factType switch
        {
            FactType.CharacterState => 5,
            FactType.LocationState => 4,
            FactType.Event => 3,
            FactType.Relationship => 2,
            FactType.Knowledge => 1,
            _ => 0
        };
    }
}

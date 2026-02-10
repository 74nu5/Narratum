using Narratum.Core;

namespace Narratum.Orchestration.Agents;

/// <summary>
/// Style narratif pour la génération.
/// </summary>
public enum NarrativeStyle
{
    /// <summary>
    /// Prose riche et détaillée.
    /// </summary>
    Descriptive,

    /// <summary>
    /// Rythme rapide, orienté action.
    /// </summary>
    Action,

    /// <summary>
    /// Focus sur les pensées et émotions.
    /// </summary>
    Introspective,

    /// <summary>
    /// Centré sur les dialogues.
    /// </summary>
    Dialogue
}

/// <summary>
/// Ton émotionnel pour les dialogues.
/// </summary>
public enum EmotionalTone
{
    /// <summary>
    /// Ton neutre, factuel.
    /// </summary>
    Neutral,

    /// <summary>
    /// Ton amical, chaleureux.
    /// </summary>
    Friendly,

    /// <summary>
    /// Ton hostile, agressif.
    /// </summary>
    Hostile,

    /// <summary>
    /// Ton craintif, anxieux.
    /// </summary>
    Fearful,

    /// <summary>
    /// Ton excité, enthousiaste.
    /// </summary>
    Excited,

    /// <summary>
    /// Ton triste, mélancolique.
    /// </summary>
    Sad
}

/// <summary>
/// Sévérité d'un problème de cohérence.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Problème mineur, peut être ignoré.
    /// </summary>
    Minor,

    /// <summary>
    /// Problème modéré, devrait être corrigé.
    /// </summary>
    Moderate,

    /// <summary>
    /// Problème sévère, doit être corrigé.
    /// </summary>
    Severe
}

/// <summary>
/// Situation de dialogue entre personnages.
/// </summary>
/// <param name="Context">Contexte de la conversation.</param>
/// <param name="Tone">Ton émotionnel dominant.</param>
/// <param name="TopicsToAddress">Sujets à aborder.</param>
public sealed record DialogueSituation(
    string Context,
    EmotionalTone Tone,
    IReadOnlyList<string> TopicsToAddress)
{
    /// <summary>
    /// Crée une situation de dialogue neutre.
    /// </summary>
    public static DialogueSituation Neutral(string context, params string[] topics)
        => new(context, EmotionalTone.Neutral, topics);

    /// <summary>
    /// Crée une situation de dialogue amicale.
    /// </summary>
    public static DialogueSituation Friendly(string context, params string[] topics)
        => new(context, EmotionalTone.Friendly, topics);

    /// <summary>
    /// Crée une situation de dialogue tendue.
    /// </summary>
    public static DialogueSituation Tense(string context, params string[] topics)
        => new(context, EmotionalTone.Hostile, topics);
}

/// <summary>
/// Résultat d'une vérification de cohérence.
/// </summary>
/// <param name="IsConsistent">Indique si le texte est cohérent.</param>
/// <param name="Issues">Liste des problèmes détectés.</param>
/// <param name="ConfidenceScore">Score de confiance (0.0-1.0).</param>
public sealed record ConsistencyCheck(
    bool IsConsistent,
    IReadOnlyList<ConsistencyIssue> Issues,
    double ConfidenceScore)
{
    /// <summary>
    /// Résultat cohérent sans problème.
    /// </summary>
    public static ConsistencyCheck Consistent()
        => new(true, Array.Empty<ConsistencyIssue>(), 1.0);

    /// <summary>
    /// Résultat avec problèmes.
    /// </summary>
    public static ConsistencyCheck WithIssues(params ConsistencyIssue[] issues)
        => new(issues.All(i => i.Severity != IssueSeverity.Severe), issues, 0.5);
}

/// <summary>
/// Problème de cohérence détecté.
/// </summary>
/// <param name="Description">Description du problème.</param>
/// <param name="ProblematicText">Texte problématique.</param>
/// <param name="SuggestedFix">Correction suggérée.</param>
/// <param name="Severity">Sévérité du problème.</param>
public sealed record ConsistencyIssue(
    string Description,
    string ProblematicText,
    string? SuggestedFix,
    IssueSeverity Severity)
{
    /// <summary>
    /// Crée un problème mineur.
    /// </summary>
    public static ConsistencyIssue Minor(string description, string text)
        => new(description, text, null, IssueSeverity.Minor);

    /// <summary>
    /// Crée un problème modéré.
    /// </summary>
    public static ConsistencyIssue Moderate(string description, string text, string? fix = null)
        => new(description, text, fix, IssueSeverity.Moderate);

    /// <summary>
    /// Crée un problème sévère.
    /// </summary>
    public static ConsistencyIssue Severe(string description, string text, string? fix = null)
        => new(description, text, fix, IssueSeverity.Severe);
}

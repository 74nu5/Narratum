using Narratum.Core;
using Narratum.Domain;
using Narratum.State;

namespace Narratum.Orchestration.Agents;

/// <summary>
/// Agent spécialisé dans la génération de résumés.
///
/// Responsabilités :
/// - Résumer des séquences d'événements
/// - Créer des résumés de chapitres
/// - Extraire les points clés d'une période narrative
/// </summary>
public interface ISummaryAgent : IAgent
{
    /// <summary>
    /// Résume une liste d'événements.
    /// </summary>
    /// <param name="events">Événements à résumer.</param>
    /// <param name="targetLength">Longueur cible du résumé en mots.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résumé textuel des événements.</returns>
    Task<Result<string>> SummarizeEventsAsync(
        IReadOnlyList<Event> events,
        int targetLength = 150,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Résume un chapitre complet.
    /// </summary>
    /// <param name="chapter">Chapitre à résumer.</param>
    /// <param name="chapterEvents">Événements du chapitre.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résumé du chapitre.</returns>
    Task<Result<string>> SummarizeChapterAsync(
        StoryChapter chapter,
        IReadOnlyList<Event> chapterEvents,
        CancellationToken cancellationToken = default);
}

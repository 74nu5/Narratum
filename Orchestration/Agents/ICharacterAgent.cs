using Narratum.Core;
using Narratum.Domain;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Agents;

/// <summary>
/// Agent spécialisé dans la génération de dialogues et réactions de personnages.
///
/// Responsabilités :
/// - Générer des dialogues cohérents avec la personnalité
/// - Créer des réactions aux événements
/// - Maintenir la voix distinctive de chaque personnage
/// </summary>
public interface ICharacterAgent : IAgent
{
    /// <summary>
    /// Génère un dialogue pour un personnage.
    /// </summary>
    /// <param name="speaker">Personnage qui parle.</param>
    /// <param name="listener">Personnage qui écoute (optionnel).</param>
    /// <param name="situation">Contexte du dialogue.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Dialogue généré.</returns>
    Task<Result<string>> GenerateDialogueAsync(
        CharacterContext speaker,
        CharacterContext? listener,
        DialogueSituation situation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère une réaction à un événement.
    /// </summary>
    /// <param name="character">Personnage qui réagit.</param>
    /// <param name="triggeringEvent">Événement déclencheur.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Réaction du personnage.</returns>
    Task<Result<string>> GenerateReactionAsync(
        CharacterContext character,
        Event triggeringEvent,
        CancellationToken cancellationToken = default);
}

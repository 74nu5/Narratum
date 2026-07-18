using Narratum.Core;

namespace Narratum.Orchestration.Llm;

/// <summary>Résultat d'une génération d'image : les octets et l'extension de fichier détectée.</summary>
public sealed record ImageResult(byte[] Bytes, string FileExtension);

/// <summary>
/// Abstraction de génération d'image (prompt → image). Comme <see cref="ILlmClient"/> pour le texte,
/// permet de brancher plusieurs providers ; aujourd'hui seule une implémentation cloud (Azure AI
/// Foundry) existe — le local est repoussé mais l'abstraction est prête à l'accueillir.
/// </summary>
public interface IImageGenerator
{
    /// <summary>Vrai si ce générateur peut servir le modèle demandé.</summary>
    bool CanHandle(string? modelId);

    /// <summary>Génère une image pour un prompt et un modèle donnés.</summary>
    Task<Result<ImageResult>> GenerateAsync(string prompt, string modelId, CancellationToken cancellationToken = default);
}

namespace Narratum.Orchestration.Llm;

/// <summary>
/// A model as the local provider actually exposes it, with the exact identifiers to use.
/// <paramref name="Id"/> is the concrete, variant-specific id (e.g.
/// "mistral-nemo-12b-instruct-generic-gpu:1") — use it to load and request the model.
/// </summary>
public record LlmModelInfo(
    string Id,
    string Alias,
    string DisplayName,
    bool Cached,
    string? Task,
    string? Device);

/// <summary>
/// Optional capability: an LLM client that can list the models the local provider knows,
/// so the UI offers real ids/variants instead of guessed ones.
/// </summary>
public interface IModelCatalogProvider
{
    Task<IReadOnlyList<LlmModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
}

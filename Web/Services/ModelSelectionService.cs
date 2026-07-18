using Narratum.Llm.Configuration;
using Narratum.Orchestration.Stages;
using Narratum.Web.Models;

namespace Narratum.Web.Services;

/// <summary>
/// Single source of truth for LLM model selection in the Web app:
/// the catalogue of available models, the default, and per-agent resolution.
///
/// The model actually used for a page is chosen by the user (at story creation or
/// per page) and travels with the request via the "llm.model" metadata key — this
/// service just supplies the catalogue and the fallback default.
/// </summary>
public class ModelSelectionService : IModelResolver
{
    private readonly LlmClientConfig _baseConfig;
    private string? _narratorModelOverride;

    public ModelSelectionService(LlmClientConfig baseConfig)
    {
        _baseConfig = baseConfig;
    }

    /// <summary>
    /// The models offered in the UI. Edit this one list to add/remove models
    /// (they must be available in your Foundry Local install to actually run).
    /// </summary>
    public static IReadOnlyList<ModelOption> AvailableModels { get; } = new List<ModelOption>
    {
        new("phi-4-mini", "Phi-4-mini — rapide, recommandé"),
        new("phi-4", "Phi-4 — plus précis"),
        new("qwen2.5-1.5b-instruct", "Qwen2.5 1.5B — léger"),
        new("mistral-7b-instruct-v0.2", "Mistral 7B Instruct"),
        new("llama-3.2-3b-instruct", "Llama 3.2 3B"),
    };

    /// <summary>
    /// The fallback model, used when nothing more specific is chosen.
    /// </summary>
    public string DefaultModel => _narratorModelOverride ?? _baseConfig.NarratorModel ?? _baseConfig.DefaultModel;

    /// <summary>
    /// Returns a usable model id: the given one, or the default when it is empty/"N/A".
    /// </summary>
    public string NormalizeOrDefault(string? model)
        => string.IsNullOrWhiteSpace(model) || model == "N/A" ? DefaultModel : model;

    /// <summary>
    /// Global narrator model override (kept for the Config page). The per-story /
    /// per-page flow passes the model explicitly and does not depend on this.
    /// </summary>
    public string CurrentNarratorModel
    {
        get => _narratorModelOverride ?? _baseConfig.NarratorModel ?? _baseConfig.DefaultModel;
        set => _narratorModelOverride = value;
    }

    /// <inheritdoc />
    public string ResolveModel(AgentType agentType)
    {
        if (agentType == AgentType.Narrator && _narratorModelOverride != null)
            return _narratorModelOverride;

        return _baseConfig.ResolveModel(agentType);
    }
}

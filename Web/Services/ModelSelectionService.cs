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
        // Curated for French narrative generation (fast → highest quality).
        // Ids match Foundry Local; a model downloads on first use if not cached.
        new("phi-4-mini", "Phi-4-mini — 3.6 Go · rapide (NPU) · défaut"),
        new("qwen2.5-7b", "Qwen2.5 7B — 4.2 Go · polyvalent, bon en français (NPU)"),
        new("qwen3-4b", "Qwen3 4B — 2.9 Go · récent, bon compromis (GPU)"),
        new("qwen3-8b", "Qwen3 8B — 6 Go · récent, qualité (GPU)"),
        new("mistral-nemo-12b-instruct", "Mistral Nemo 12B — 7.3 Go · excellent en français (GPU)"),
        new("phi-4", "Phi-4 — 8.8 Go · très cohérent (GPU)"),
        new("gpt-oss-20b", "GPT-OSS 20B — 11.8 Go · qualité max, plus lent (GPU)"),
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

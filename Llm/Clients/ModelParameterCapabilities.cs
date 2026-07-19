using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Narratum.Llm.Clients;

/// <summary>The sampling knobs a request can carry. Models reject them individually.</summary>
[Flags]
public enum SamplingParameter
{
    None = 0,
    Temperature = 1 << 0,
    TopP = 1 << 1,
    MaxOutputTokens = 1 << 2,
    StopSequences = 1 << 3,
    All = Temperature | TopP | MaxOutputTokens | StopSequences,
}

/// <summary>
/// Which sampling parameters each model refuses, so requests are built right the first time.
/// <para>
/// No provider publishes this: the Azure deployment listing gives a model name and format, never
/// « accepts a temperature ». The only reliable source is the rejection itself — so this learns
/// from 400/422 responses and remembers, process-wide. A short table of known families pre-seeds
/// it so even the first call to a reasoning model skips the round trip; anything wrong in that
/// table is corrected by the first real rejection.
/// </para>
/// </summary>
public sealed class ModelParameterCapabilities
{
    private readonly ConcurrentDictionary<string, SamplingParameter> _unsupported =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger _logger;

    public ModelParameterCapabilities(ILogger<ModelParameterCapabilities>? logger = null)
        => this._logger = logger ?? NullLogger<ModelParameterCapabilities>.Instance;

    /// <summary>
    /// Wire names as providers spell them in error messages. Several map to the same knob
    /// (<c>max_tokens</c> / <c>max_completion_tokens</c>), which costs nothing since we only
    /// care about which of our options to drop.
    /// </summary>
    private static readonly (string Wire, SamplingParameter Flag)[] WireNames =
    [
        ("temperature", SamplingParameter.Temperature),
        ("top_p", SamplingParameter.TopP),
        ("max_completion_tokens", SamplingParameter.MaxOutputTokens),
        ("max_output_tokens", SamplingParameter.MaxOutputTokens),
        ("max_tokens", SamplingParameter.MaxOutputTokens),
        ("stop", SamplingParameter.StopSequences),
    ];

    /// <summary>
    /// Reasoning models (GPT-5 and the o-series) only accept the default temperature and top-p.
    /// Deployment names are author-chosen, so this matches loosely on purpose — a false positive
    /// costs one un-tuned model, never a failed call, and a false negative just falls back to
    /// learning.
    /// </summary>
    private static readonly Regex ReasoningFamily =
        new(@"(^|[^a-z0-9])(gpt-?5|o[1345])([^a-z0-9]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Some Mistral deployments reject an explicit output-token cap.</summary>
    private static readonly Regex MistralFamily =
        new(@"mistral", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The parameters to leave out of a request for this model.</summary>
    public SamplingParameter GetUnsupported(string modelId)
        => string.IsNullOrWhiteSpace(modelId)
            ? SamplingParameter.None
            : this._unsupported.GetOrAdd(modelId, Seed);

    /// <summary>
    /// Records what a model just refused. Returns <c>true</c> only when this is <em>new</em>
    /// information — which is exactly the condition for a retry being worth attempting, and what
    /// stops a model that keeps refusing from looping forever.
    /// </summary>
    public bool Learn(string modelId, string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return false;

        var refused = Parse(errorMessage);
        var before = this.GetUnsupported(modelId);
        var after = before | refused;

        if (after == before)
            return false;

        this._unsupported[modelId] = after;
        this._logger.LogInformation(
            "Model {Model} refuses {Refused}; future requests omit {Unsupported}",
            modelId, refused, after);

        return true;
    }

    /// <summary>
    /// Reads the refused parameter out of the provider's message. Only quoted names count
    /// (<c>Unsupported value: 'temperature' does not support…</c>) — the bare word « stop »
    /// shows up in ordinary prose. When nothing is recognised we drop everything, which is the
    /// safe interpretation and matches what a request with no options at all would do.
    /// </summary>
    private static SamplingParameter Parse(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return SamplingParameter.All;

        var refused = SamplingParameter.None;
        foreach (var (wire, flag) in WireNames)
        {
            if (errorMessage.Contains($"'{wire}'", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains($"\"{wire}\"", StringComparison.OrdinalIgnoreCase))
            {
                refused |= flag;
            }
        }

        return refused == SamplingParameter.None ? SamplingParameter.All : refused;
    }

    /// <summary>What we assume before the model has ever answered us.</summary>
    private static SamplingParameter Seed(string modelId)
    {
        if (ReasoningFamily.IsMatch(modelId))
            return SamplingParameter.Temperature | SamplingParameter.TopP;

        if (MistralFamily.IsMatch(modelId))
            return SamplingParameter.MaxOutputTokens;

        return SamplingParameter.None;
    }
}

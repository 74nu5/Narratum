using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Configuration;

/// <summary>
/// Configuration for temperature settings per agent type.
/// Controls creativity/determinism trade-off for each specialized agent.
/// </summary>
public sealed record AgentTemperatureConfig
{
    /// <summary>
    /// Temperature for Narrator agent (narrative generation).
    /// Higher = more creative, varied prose.
    /// </summary>
    public double NarratorTemperature { get; init; } = 0.7;

    /// <summary>
    /// Temperature for Character agent (dialogue generation).
    /// Higher = more personality variation.
    /// </summary>
    public double CharacterTemperature { get; init; } = 0.8;

    /// <summary>
    /// Temperature for Summary agent (event summarization).
    /// Lower = more consistent, factual summaries.
    /// </summary>
    public double SummaryTemperature { get; init; } = 0.3;

    /// <summary>
    /// Temperature for Consistency agent (fact checking).
    /// Lower = more deterministic, reliable validation.
    /// </summary>
    public double ConsistencyTemperature { get; init; } = 0.1;

    /// <summary>
    /// Temperature for Choice agent (proposing next-step options).
    /// Moderately high for varied, distinct options while staying on-topic.
    /// </summary>
    public double ChoiceTemperature { get; init; } = 0.8;

    /// <summary>
    /// Temperature for Secret agent (tracking revealed/hidden information).
    /// Low-ish: secrets should follow from the text, not be invented wildly.
    /// </summary>
    public double SecretTemperature { get; init; } = 0.4;

    /// <summary>
    /// Temperature for the ImagePrompt agent (page text → visual prompt).
    /// Moderate for evocative but faithful descriptions.
    /// </summary>
    public double ImagePromptTemperature { get; init; } = 0.6;

    /// <summary>
    /// Default configuration with balanced temperatures.
    /// </summary>
    public static AgentTemperatureConfig Default => new();

    /// <summary>
    /// Conservative configuration (lower temperatures, more deterministic).
    /// </summary>
    public static AgentTemperatureConfig Conservative => new()
    {
        NarratorTemperature = 0.5,
        CharacterTemperature = 0.6,
        SummaryTemperature = 0.2,
        ConsistencyTemperature = 0.0
    };

    /// <summary>
    /// Creative configuration (higher temperatures, more varied).
    /// </summary>
    public static AgentTemperatureConfig Creative => new()
    {
        NarratorTemperature = 0.9,
        CharacterTemperature = 1.0,
        SummaryTemperature = 0.4,
        ConsistencyTemperature = 0.1
    };

    /// <summary>
    /// Gets the temperature for a specific agent type.
    /// </summary>
    public double GetTemperature(AgentType agentType) => agentType switch
    {
        AgentType.Narrator => NarratorTemperature,
        AgentType.Character => CharacterTemperature,
        AgentType.Summary => SummaryTemperature,
        AgentType.Consistency => ConsistencyTemperature,
        AgentType.Choice => ChoiceTemperature,
        AgentType.Secret => SecretTemperature,
        AgentType.ImagePrompt => ImagePromptTemperature,
        _ => 0.7 // Default fallback
    };

    /// <summary>
    /// Validates that all temperatures are within valid range [0.0, 2.0].
    /// </summary>
    public bool IsValid()
    {
        return IsValidTemperature(NarratorTemperature)
            && IsValidTemperature(CharacterTemperature)
            && IsValidTemperature(SummaryTemperature)
            && IsValidTemperature(ConsistencyTemperature)
            && IsValidTemperature(ChoiceTemperature)
            && IsValidTemperature(SecretTemperature)
            && IsValidTemperature(ImagePromptTemperature);
    }

    private static bool IsValidTemperature(double temp)
        => temp >= 0.0 && temp <= 2.0;
}

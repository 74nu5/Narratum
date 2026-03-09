namespace Narratum.Orchestration.Services;

/// <summary>
/// Configuration de l'orchestration narrative.
/// </summary>
public sealed record OrchestrationConfig
{
    /// <summary>
    /// Nombre maximum de tentatives en cas d'échec.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Timeout pour chaque étape du pipeline.
    /// </summary>
    public TimeSpan StageTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout global pour l'exécution complète.
    /// </summary>
    public TimeSpan GlobalTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Active le logging détaillé.
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;

    /// <summary>
    /// Utilise les agents mock.
    /// </summary>
    public bool UseMockAgents { get; init; } = true;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static OrchestrationConfig Default => new();

    /// <summary>
    /// Configuration pour les tests.
    /// </summary>
    public static OrchestrationConfig ForTesting => new()
    {
        MaxRetries = 1,
        StageTimeout = TimeSpan.FromSeconds(5),
        GlobalTimeout = TimeSpan.FromSeconds(30),
        EnableDetailedLogging = true,
        UseMockAgents = true
    };
}

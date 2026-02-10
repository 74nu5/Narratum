using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Validation;

/// <summary>
/// Interface pour définir une politique de retry.
///
/// Permet de personnaliser le comportement de retry
/// lors des échecs de validation.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Nombre maximum de tentatives.
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    /// Détermine si une nouvelle tentative doit être effectuée.
    /// </summary>
    /// <param name="attemptNumber">Numéro de la tentative actuelle (commence à 1).</param>
    /// <param name="context">Contexte de la tentative.</param>
    /// <returns>True si une nouvelle tentative doit être effectuée.</returns>
    bool ShouldRetry(int attemptNumber, RetryContext context);

    /// <summary>
    /// Obtient le délai avant la prochaine tentative.
    /// </summary>
    /// <param name="attemptNumber">Numéro de la tentative actuelle.</param>
    /// <returns>Le délai à attendre.</returns>
    TimeSpan GetDelay(int attemptNumber);

    /// <summary>
    /// Appelé avant chaque tentative de retry.
    /// </summary>
    /// <param name="attemptNumber">Numéro de la tentative.</param>
    /// <param name="context">Contexte de la tentative.</param>
    void OnRetry(int attemptNumber, RetryContext context);
}

/// <summary>
/// Contexte d'une tentative de retry.
/// </summary>
public sealed record RetryContext(
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyList<string> WarningMessages,
    TimeSpan ElapsedTime,
    IReadOnlyDictionary<string, object> Metadata)
{
    public static RetryContext Empty => new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        TimeSpan.Zero,
        new Dictionary<string, object>());

    public static RetryContext FromValidationResult(ValidationResult result, TimeSpan elapsed)
        => new(
            result.ErrorMessages.ToList(),
            result.Warnings.Select(w => w.Message).ToList(),
            elapsed,
            new Dictionary<string, object>(result.Metadata));

    public bool HasCriticalErrors => ErrorMessages.Count > 0;
}

/// <summary>
/// Politique de retry simple avec nombre fixe de tentatives.
/// </summary>
public sealed class SimpleRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;
    private readonly Action<int, RetryContext>? _onRetry;

    public SimpleRetryPolicy(
        int maxRetries = 3,
        TimeSpan? delay = null,
        Action<int, RetryContext>? onRetry = null)
    {
        _maxRetries = maxRetries;
        _delay = delay ?? TimeSpan.Zero;
        _onRetry = onRetry;
    }

    public int MaxRetries => _maxRetries;

    public bool ShouldRetry(int attemptNumber, RetryContext context)
        => attemptNumber <= _maxRetries && context.HasCriticalErrors;

    public TimeSpan GetDelay(int attemptNumber)
        => _delay;

    public void OnRetry(int attemptNumber, RetryContext context)
        => _onRetry?.Invoke(attemptNumber, context);
}

/// <summary>
/// Politique de retry avec backoff exponentiel.
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _multiplier;
    private readonly TimeSpan _maxDelay;
    private readonly Action<int, RetryContext>? _onRetry;

    public ExponentialBackoffRetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double multiplier = 2.0,
        TimeSpan? maxDelay = null,
        Action<int, RetryContext>? onRetry = null)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        _multiplier = multiplier;
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(10);
        _onRetry = onRetry;
    }

    public int MaxRetries => _maxRetries;

    public bool ShouldRetry(int attemptNumber, RetryContext context)
        => attemptNumber <= _maxRetries && context.HasCriticalErrors;

    public TimeSpan GetDelay(int attemptNumber)
    {
        var delay = TimeSpan.FromTicks(
            (long)(_initialDelay.Ticks * Math.Pow(_multiplier, attemptNumber - 1)));

        return delay > _maxDelay ? _maxDelay : delay;
    }

    public void OnRetry(int attemptNumber, RetryContext context)
        => _onRetry?.Invoke(attemptNumber, context);
}

/// <summary>
/// Politique de retry conditionnelle basée sur le type d'erreur.
/// </summary>
public sealed class ConditionalRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly Func<RetryContext, bool> _condition;
    private readonly TimeSpan _delay;
    private readonly Action<int, RetryContext>? _onRetry;

    public ConditionalRetryPolicy(
        Func<RetryContext, bool> condition,
        int maxRetries = 3,
        TimeSpan? delay = null,
        Action<int, RetryContext>? onRetry = null)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _maxRetries = maxRetries;
        _delay = delay ?? TimeSpan.Zero;
        _onRetry = onRetry;
    }

    public int MaxRetries => _maxRetries;

    public bool ShouldRetry(int attemptNumber, RetryContext context)
        => attemptNumber <= _maxRetries && _condition(context);

    public TimeSpan GetDelay(int attemptNumber)
        => _delay;

    public void OnRetry(int attemptNumber, RetryContext context)
        => _onRetry?.Invoke(attemptNumber, context);

    /// <summary>
    /// Crée une politique qui retry uniquement pour certains types d'erreurs.
    /// </summary>
    public static ConditionalRetryPolicy ForErrors(
        IEnumerable<string> retryableErrorPatterns,
        int maxRetries = 3)
    {
        var patterns = retryableErrorPatterns.ToList();
        return new ConditionalRetryPolicy(
            context => context.ErrorMessages.Any(e =>
                patterns.Any(p => e.Contains(p, StringComparison.OrdinalIgnoreCase))),
            maxRetries);
    }
}

/// <summary>
/// Politique qui ne fait jamais de retry.
/// </summary>
public sealed class NoRetryPolicy : IRetryPolicy
{
    public static NoRetryPolicy Instance { get; } = new();

    private NoRetryPolicy() { }

    public int MaxRetries => 0;

    public bool ShouldRetry(int attemptNumber, RetryContext context) => false;

    public TimeSpan GetDelay(int attemptNumber) => TimeSpan.Zero;

    public void OnRetry(int attemptNumber, RetryContext context) { }
}

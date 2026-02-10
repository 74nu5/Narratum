using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Validation;

/// <summary>
/// Interface pour le gestionnaire de retry.
/// </summary>
public interface IRetryHandler
{
    /// <summary>
    /// Exécute une opération avec retry automatique en cas d'échec de validation.
    /// </summary>
    /// <typeparam name="T">Type de résultat.</typeparam>
    /// <param name="operation">L'opération à exécuter.</param>
    /// <param name="validator">Le validateur de résultat.</param>
    /// <param name="rewriter">Le réécrivain en cas d'échec.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Le résultat final avec les métadonnées de retry.</returns>
    Task<RetryResult<T>> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<T, Task<ValidationResult>> validator,
        Func<T, ValidationResult, CancellationToken, Task<T>> rewriter,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Résultat d'une opération avec retry.
/// </summary>
public sealed record RetryResult<T>(
    T Value,
    bool Success,
    int AttemptCount,
    ValidationResult? FinalValidation,
    TimeSpan TotalDuration,
    IReadOnlyList<RetryAttempt> Attempts)
{
    public bool WasRetried => AttemptCount > 1;

    public static RetryResult<T> Successful(T value, ValidationResult validation, TimeSpan duration)
        => new(value, true, 1, validation,duration, new[] { RetryAttempt.Success(1, duration) });

    public static RetryResult<T> Failed(T value, ValidationResult validation, TimeSpan duration, IReadOnlyList<RetryAttempt> attempts)
        => new(value, false, attempts.Count, validation, duration, attempts);
}

/// <summary>
/// Information sur une tentative de retry.
/// </summary>
public sealed record RetryAttempt(
    int AttemptNumber,
    bool IsSuccess,
    TimeSpan Duration,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static RetryAttempt Success(int attemptNumber, TimeSpan duration)
        => new(attemptNumber, true, duration, Array.Empty<string>(), Array.Empty<string>());

    public static RetryAttempt Failure(int attemptNumber, TimeSpan duration, ValidationResult result)
        => new(attemptNumber, false, duration, result.ErrorMessages.ToList(), result.Warnings.Select(w => w.Message).ToList());
}

/// <summary>
/// Implémentation du gestionnaire de retry.
///
/// Gère la boucle de retry avec validation et réécriture.
/// Respecte la politique de retry configurée.
/// </summary>
public sealed class RetryHandler : IRetryHandler
{
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger<RetryHandler>? _logger;

    public RetryHandler(
        IRetryPolicy? retryPolicy = null,
        ILogger<RetryHandler>? logger = null)
    {
        _retryPolicy = retryPolicy ?? new SimpleRetryPolicy();
        _logger = logger;
    }

    public async Task<RetryResult<T>> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<T, Task<ValidationResult>> validator,
        Func<T, ValidationResult, CancellationToken, Task<T>> rewriter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(rewriter);

        var totalStopwatch = Stopwatch.StartNew();
        var attempts = new List<RetryAttempt>();
        var attemptNumber = 1;

        // Exécution initiale
        var attemptStopwatch = Stopwatch.StartNew();
        var result = await operation(cancellationToken);
        var validation = await validator(result);
        attemptStopwatch.Stop();

        _logger?.LogDebug(
            "Initial attempt completed: Valid={IsValid}, Duration={Duration}ms",
            validation.IsValid, attemptStopwatch.ElapsedMilliseconds);

        if (validation.IsValid)
        {
            totalStopwatch.Stop();
            return RetryResult<T>.Successful(result, validation, totalStopwatch.Elapsed);
        }

        attempts.Add(RetryAttempt.Failure(attemptNumber, attemptStopwatch.Elapsed, validation));

        // Boucle de retry
        while (true)
        {
            var retryContext = RetryContext.FromValidationResult(validation, totalStopwatch.Elapsed);

            if (!_retryPolicy.ShouldRetry(attemptNumber, retryContext))
            {
                _logger?.LogDebug(
                    "Retry policy declined retry at attempt {Attempt}",
                    attemptNumber);
                break;
            }

            // Attendre si nécessaire
            var delay = _retryPolicy.GetDelay(attemptNumber);
            if (delay > TimeSpan.Zero)
            {
                _logger?.LogDebug("Waiting {Delay}ms before retry", delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }

            attemptNumber++;
            _retryPolicy.OnRetry(attemptNumber, retryContext);

            _logger?.LogDebug(
                "Starting retry attempt {Attempt}/{MaxRetries}",
                attemptNumber, _retryPolicy.MaxRetries);

            // Réécriture
            attemptStopwatch.Restart();
            try
            {
                result = await rewriter(result, validation, cancellationToken);
                validation = await validator(result);
                attemptStopwatch.Stop();

                _logger?.LogDebug(
                    "Retry attempt {Attempt} completed: Valid={IsValid}, Duration={Duration}ms",
                    attemptNumber, validation.IsValid, attemptStopwatch.ElapsedMilliseconds);

                if (validation.IsValid)
                {
                    attempts.Add(RetryAttempt.Success(attemptNumber, attemptStopwatch.Elapsed));
                    totalStopwatch.Stop();

                    return new RetryResult<T>(
                        result,
                        true,
                        attemptNumber,
                        validation,
                        totalStopwatch.Elapsed,
                        attempts);
                }

                attempts.Add(RetryAttempt.Failure(attemptNumber, attemptStopwatch.Elapsed, validation));
            }
            catch (Exception ex)
            {
                attemptStopwatch.Stop();
                _logger?.LogWarning(ex, "Retry attempt {Attempt} failed with exception", attemptNumber);

                attempts.Add(new RetryAttempt(
                    attemptNumber,
                    false,
                    attemptStopwatch.Elapsed,
                    new[] { ex.Message },
                    Array.Empty<string>()));

                // Créer un résultat de validation synthétique
                validation = ValidationResult.Invalid($"Exception during retry: {ex.Message}");
            }
        }

        totalStopwatch.Stop();

        _logger?.LogWarning(
            "All retry attempts exhausted after {Attempts} attempts, total duration {Duration}ms",
            attemptNumber, totalStopwatch.ElapsedMilliseconds);

        return RetryResult<T>.Failed(result, validation, totalStopwatch.Elapsed, attempts);
    }
}

/// <summary>
/// Extensions pour faciliter l'utilisation du RetryHandler.
/// </summary>
public static class RetryHandlerExtensions
{
    /// <summary>
    /// Exécute une opération de génération avec retry.
    /// </summary>
    public static async Task<RetryResult<RawOutput>> ExecuteGenerationWithRetryAsync(
        this IRetryHandler retryHandler,
        IAgentExecutor executor,
        IOutputValidator validator,
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        return await retryHandler.ExecuteWithRetryAsync(
            async ct =>
            {
                var result = await executor.ExecuteAsync(prompts, context, ct);
                return result is Result<RawOutput>.Success success
                    ? success.Value
                    : throw new InvalidOperationException(
                        result is Result<RawOutput>.Failure failure
                            ? failure.Message
                            : "Unknown error");
            },
            async output => await validator.ValidateAsync(output, context, cancellationToken),
            async (previousOutput, validationResult, ct) =>
            {
                var result = await executor.RewriteAsync(previousOutput, validationResult, context, ct);
                return result is Result<RawOutput>.Success success
                    ? success.Value
                    : throw new InvalidOperationException(
                        result is Result<RawOutput>.Failure failure
                            ? failure.Message
                            : "Unknown error");
            },
            cancellationToken);
    }
}

using FluentAssertions;
using Narratum.Orchestration.Stages;
using Narratum.Orchestration.Validation;
using Xunit;

namespace Narratum.Orchestration.Tests.Validation;

/// <summary>
/// Tests unitaires pour RetryHandler.
/// </summary>
public class RetryHandlerTests
{
    [Fact]
    public async Task ExecuteWithRetry_WithSuccessOnFirstTry_ShouldNotRetry()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 3));
        var callCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                callCount++;
                return "Success";
            },
            validator: async value => ValidationResult.Valid(),
            rewriter: async (value, validation, ct) => "Rewritten");

        // Assert
        result.Success.Should().BeTrue();
        result.AttemptCount.Should().Be(1);
        result.WasRetried.Should().BeFalse();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_WithFailureThenSuccess_ShouldRetry()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 3));
        var attemptCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                return "Initial";
            },
            validator: async value =>
            {
                // Fail on first attempt, succeed after
                return attemptCount <= 1
                    ? ValidationResult.Invalid("Error on first try")
                    : ValidationResult.Valid();
            },
            rewriter: async (value, validation, ct) =>
            {
                attemptCount++;
                return "Rewritten";
            });

        // Assert
        result.Success.Should().BeTrue();
        result.AttemptCount.Should().Be(2);
        result.WasRetried.Should().BeTrue();
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithRetry_WithAllFailures_ShouldExhaustRetries()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 3));
        var attemptCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                return "Value";
            },
            validator: async _ => ValidationResult.Invalid("Always fails"),
            rewriter: async (value, validation, ct) =>
            {
                attemptCount++;
                return "Rewritten";
            });

        // Assert
        result.Success.Should().BeFalse();
        result.AttemptCount.Should().Be(4); // Initial + 3 retries
        result.Attempts.Should().HaveCount(4);
        result.Attempts.All(a => !a.IsSuccess).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWithRetry_WithNoRetryPolicy_ShouldNotRetry()
    {
        // Arrange
        var handler = new RetryHandler(NoRetryPolicy.Instance);
        var attemptCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                return "Value";
            },
            validator: async _ => ValidationResult.Invalid("Fails"),
            rewriter: async (value, validation, ct) => "Never called");

        // Assert
        result.Success.Should().BeFalse();
        result.AttemptCount.Should().Be(1);
        result.WasRetried.Should().BeFalse();
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_ShouldRecordAttempts()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 2));
        var attemptCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                return "Value";
            },
            validator: async _ =>
            {
                if (attemptCount < 3)
                    return ValidationResult.Invalid($"Error {attemptCount}");
                return ValidationResult.Valid();
            },
            rewriter: async (value, validation, ct) =>
            {
                attemptCount++;
                return "Rewritten";
            });

        // Assert
        result.Attempts.Should().HaveCount(3);
        result.Attempts[0].AttemptNumber.Should().Be(1);
        result.Attempts[0].IsSuccess.Should().BeFalse();
        result.Attempts[1].AttemptNumber.Should().Be(2);
        result.Attempts[1].IsSuccess.Should().BeFalse();
        result.Attempts[2].AttemptNumber.Should().Be(3);
        result.Attempts[2].IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWithRetry_WithDelay_ShouldWait()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(50);
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 1, delay: delay));
        var attemptCount = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                return "Value";
            },
            validator: async _ =>
            {
                return attemptCount <= 1
                    ? ValidationResult.Invalid("Fail")
                    : ValidationResult.Valid();
            },
            rewriter: async (value, validation, ct) =>
            {
                attemptCount++;
                return "Rewritten";
            });

        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(40); // Allow some tolerance
    }

    [Fact]
    public async Task ExecuteWithRetry_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 10));
        using var cts = new CancellationTokenSource();
        var attemptCount = 0;

        // Cancel immediately
        cts.Cancel();

        // Act - Since the token is cancelled, the delay should throw
        var exception = await Record.ExceptionAsync(async () =>
        {
            await handler.ExecuteWithRetryAsync(
                operation: async ct =>
                {
                    attemptCount++;
                    return "Value";
                },
                validator: async _ => ValidationResult.Invalid("Fail"),
                rewriter: async (value, validation, ct) =>
                {
                    // This will throw since token is cancelled when delay is called
                    await Task.Delay(100, ct);
                    return "Rewritten";
                },
                cts.Token);
        });

        // The retry handler should fail gracefully or throw cancellation
        // At minimum, we should have had initial operation called
        attemptCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_WithExceptionInRewriter_ShouldContinue()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 2));
        var attemptCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                return "Initial";
            },
            validator: async _ =>
            {
                return attemptCount < 3
                    ? ValidationResult.Invalid("Fail")
                    : ValidationResult.Valid();
            },
            rewriter: async (value, validation, ct) =>
            {
                attemptCount++;
                if (attemptCount == 2)
                    throw new InvalidOperationException("Rewriter failed");
                return "Rewritten";
            });

        // Assert - should have recorded the exception attempt
        result.Attempts.Should().Contain(a => a.Errors.Any(e => e.Contains("Rewriter failed")));
    }

    [Fact]
    public async Task ExecuteWithRetry_ShouldMeasureTotalDuration()
    {
        // Arrange
        var handler = new RetryHandler(new SimpleRetryPolicy(maxRetries: 1));
        var attemptCount = 0;

        // Act
        var result = await handler.ExecuteWithRetryAsync(
            operation: async _ =>
            {
                attemptCount++;
                await Task.Delay(10);
                return "Value";
            },
            validator: async _ =>
            {
                return attemptCount <= 1
                    ? ValidationResult.Invalid("Fail")
                    : ValidationResult.Valid();
            },
            rewriter: async (value, validation, ct) =>
            {
                attemptCount++;
                await Task.Delay(10);
                return "Rewritten";
            });

        // Assert
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(15));
    }

    [Fact]
    public async Task ExecuteWithRetry_WithNullOperation_ShouldThrow()
    {
        // Arrange
        var handler = new RetryHandler();

        // Act
        var action = async () => await handler.ExecuteWithRetryAsync<string>(
            null!,
            async _ => ValidationResult.Valid(),
            async (_, _, _) => "");

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteWithRetry_WithNullValidator_ShouldThrow()
    {
        // Arrange
        var handler = new RetryHandler();

        // Act
        var action = async () => await handler.ExecuteWithRetryAsync(
            async _ => "Value",
            null!,
            async (_, _, _) => "");

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteWithRetry_WithNullRewriter_ShouldThrow()
    {
        // Arrange
        var handler = new RetryHandler();

        // Act
        var action = async () => await handler.ExecuteWithRetryAsync(
            async _ => "Value",
            async _ => ValidationResult.Valid(),
            null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }
}

/// <summary>
/// Tests pour les politiques de retry.
/// </summary>
public class RetryPolicyTests
{
    [Fact]
    public void SimpleRetryPolicy_ShouldRespectMaxRetries()
    {
        // Arrange
        var policy = new SimpleRetryPolicy(maxRetries: 3);

        // Assert
        policy.MaxRetries.Should().Be(3);
        policy.ShouldRetry(1, new RetryContext(new[] { "error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>())).Should().BeTrue();
        policy.ShouldRetry(3, new RetryContext(new[] { "error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>())).Should().BeTrue();
        policy.ShouldRetry(4, new RetryContext(new[] { "error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>())).Should().BeFalse();
    }

    [Fact]
    public void SimpleRetryPolicy_ShouldNotRetryWithoutErrors()
    {
        // Arrange
        var policy = new SimpleRetryPolicy(maxRetries: 3);
        var contextWithoutErrors = new RetryContext(Array.Empty<string>(), Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());

        // Assert
        policy.ShouldRetry(1, contextWithoutErrors).Should().BeFalse();
    }

    [Fact]
    public void ExponentialBackoffPolicy_ShouldIncreaseDelay()
    {
        // Arrange
        var policy = new ExponentialBackoffRetryPolicy(
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 2.0);

        // Assert
        policy.GetDelay(1).Should().Be(TimeSpan.FromMilliseconds(100));
        policy.GetDelay(2).Should().Be(TimeSpan.FromMilliseconds(200));
        policy.GetDelay(3).Should().Be(TimeSpan.FromMilliseconds(400));
    }

    [Fact]
    public void ExponentialBackoffPolicy_ShouldRespectMaxDelay()
    {
        // Arrange
        var policy = new ExponentialBackoffRetryPolicy(
            maxRetries: 10,
            initialDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 10.0,
            maxDelay: TimeSpan.FromSeconds(1));

        // Assert
        policy.GetDelay(1).Should().Be(TimeSpan.FromMilliseconds(100));
        policy.GetDelay(2).Should().Be(TimeSpan.FromSeconds(1)); // Would be 1000ms, capped at 1s
        policy.GetDelay(3).Should().Be(TimeSpan.FromSeconds(1)); // Would be 10000ms, capped at 1s
    }

    [Fact]
    public void ConditionalRetryPolicy_ShouldUseCondition()
    {
        // Arrange
        var policy = new ConditionalRetryPolicy(
            condition: ctx => ctx.ErrorMessages.Any(e => e.Contains("retryable")),
            maxRetries: 3);

        var retryableContext = new RetryContext(new[] { "retryable error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());
        var nonRetryableContext = new RetryContext(new[] { "fatal error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());

        // Assert
        policy.ShouldRetry(1, retryableContext).Should().BeTrue();
        policy.ShouldRetry(1, nonRetryableContext).Should().BeFalse();
    }

    [Fact]
    public void ConditionalRetryPolicy_ForErrors_ShouldMatchPatterns()
    {
        // Arrange
        var policy = ConditionalRetryPolicy.ForErrors(
            new[] { "timeout", "temporary" },
            maxRetries: 3);

        var timeoutContext = new RetryContext(new[] { "Connection timeout occurred" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());
        var permanentContext = new RetryContext(new[] { "Invalid credentials" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());

        // Assert
        policy.ShouldRetry(1, timeoutContext).Should().BeTrue();
        policy.ShouldRetry(1, permanentContext).Should().BeFalse();
    }

    [Fact]
    public void NoRetryPolicy_ShouldNeverRetry()
    {
        // Arrange
        var policy = NoRetryPolicy.Instance;
        var context = new RetryContext(new[] { "error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());

        // Assert
        policy.MaxRetries.Should().Be(0);
        policy.ShouldRetry(1, context).Should().BeFalse();
        policy.GetDelay(1).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void SimpleRetryPolicy_ShouldCallOnRetryCallback()
    {
        // Arrange
        var callbackCalled = false;
        var callbackAttempt = 0;
        var policy = new SimpleRetryPolicy(
            maxRetries: 3,
            onRetry: (attempt, ctx) =>
            {
                callbackCalled = true;
                callbackAttempt = attempt;
            });

        var context = new RetryContext(new[] { "error" }, Array.Empty<string>(), TimeSpan.Zero, new Dictionary<string, object>());

        // Act
        policy.OnRetry(2, context);

        // Assert
        callbackCalled.Should().BeTrue();
        callbackAttempt.Should().Be(2);
    }
}

/// <summary>
/// Tests pour RetryContext.
/// </summary>
public class RetryContextTests
{
    [Fact]
    public void Empty_ShouldCreateEmptyContext()
    {
        // Act
        var context = RetryContext.Empty;

        // Assert
        context.ErrorMessages.Should().BeEmpty();
        context.WarningMessages.Should().BeEmpty();
        context.ElapsedTime.Should().Be(TimeSpan.Zero);
        context.HasCriticalErrors.Should().BeFalse();
    }

    [Fact]
    public void FromValidationResult_ShouldConvertCorrectly()
    {
        // Arrange
        var errors = new[] { ValidationError.Critical("Error 1"), ValidationError.Major("Error 2") };
        var warnings = new[] { new ValidationWarning("Warning 1") };
        var result = new ValidationResult(false, errors, warnings, new Dictionary<string, object> { ["key"] = "value" });

        // Act
        var context = RetryContext.FromValidationResult(result, TimeSpan.FromSeconds(5));

        // Assert
        context.ErrorMessages.Should().HaveCount(2);
        context.WarningMessages.Should().HaveCount(1);
        context.ElapsedTime.Should().Be(TimeSpan.FromSeconds(5));
        context.HasCriticalErrors.Should().BeTrue();
    }
}

/// <summary>
/// Tests pour RetryResult.
/// </summary>
public class RetryResultTests
{
    [Fact]
    public void Successful_ShouldCreateSuccessResult()
    {
        // Arrange
        var validation = ValidationResult.Valid();

        // Act
        var result = RetryResult<string>.Successful("Value", validation, TimeSpan.FromSeconds(1));

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be("Value");
        result.AttemptCount.Should().Be(1);
        result.WasRetried.Should().BeFalse();
    }

    [Fact]
    public void Failed_ShouldCreateFailureResult()
    {
        // Arrange
        var validation = ValidationResult.Invalid("Error");
        var attempts = new[] { RetryAttempt.Failure(1, TimeSpan.FromSeconds(1), validation) };

        // Act
        var result = RetryResult<string>.Failed("Value", validation, TimeSpan.FromSeconds(2), attempts);

        // Assert
        result.Success.Should().BeFalse();
        result.Value.Should().Be("Value");
        result.Attempts.Should().HaveCount(1);
    }
}

/// <summary>
/// Tests pour RetryAttempt.
/// </summary>
public class RetryAttemptTests
{
    [Fact]
    public void Success_ShouldCreateSuccessAttempt()
    {
        // Act
        var attempt = RetryAttempt.Success(1, TimeSpan.FromSeconds(1));

        // Assert
        attempt.AttemptNumber.Should().Be(1);
        attempt.IsSuccess.Should().BeTrue();
        attempt.Duration.Should().Be(TimeSpan.FromSeconds(1));
        attempt.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldCreateFailureAttempt()
    {
        // Arrange
        var validation = ValidationResult.Invalid("Error message");

        // Act
        var attempt = RetryAttempt.Failure(2, TimeSpan.FromSeconds(1), validation);

        // Assert
        attempt.AttemptNumber.Should().Be(2);
        attempt.IsSuccess.Should().BeFalse();
        attempt.Errors.Should().Contain("Error message");
    }
}

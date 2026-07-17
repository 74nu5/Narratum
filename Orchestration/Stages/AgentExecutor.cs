using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Narratum.Core;
using Narratum.Orchestration.Llm;

namespace Narratum.Orchestration.Stages;

/// <summary>
/// Implémentation de l'AgentExecutor.
///
/// Exécute les prompts sur les agents via le client LLM
/// et collecte les réponses.
/// </summary>
public class AgentExecutor : IAgentExecutor
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<AgentExecutor>? _logger;

    public AgentExecutor(
        ILlmClient llmClient,
        ILogger<AgentExecutor>? logger = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger;
    }

    public async Task<Result<RawOutput>> ExecuteAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(context);

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug(
                "Executing {Count} prompts with {Order} order",
                prompts.Prompts.Count, prompts.Order);

            var responses = prompts.Order switch
            {
                ExecutionOrder.Parallel => await ExecuteParallelAsync(prompts, context, cancellationToken),
                ExecutionOrder.Sequential => await ExecuteSequentialAsync(prompts, context, cancellationToken),
                ExecutionOrder.Conditional => await ExecuteConditionalAsync(prompts, context, cancellationToken),
                _ => await ExecuteSequentialAsync(prompts, context, cancellationToken)
            };

            totalStopwatch.Stop();

            var output = RawOutput.Create(responses, totalStopwatch.Elapsed);

            _logger?.LogDebug(
                "Execution completed in {Duration}ms, {SuccessCount}/{TotalCount} successful",
                totalStopwatch.ElapsedMilliseconds,
                responses.Count(r => r.Success),
                responses.Count);

            return Result<RawOutput>.Ok(output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // User cancellation - propagate
        }
        catch (InvalidOperationException ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent execution failed: invalid state");
            return Result<RawOutput>.Fail($"Invalid operation: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent execution failed: invalid argument");
            return Result<RawOutput>.Fail($"Invalid argument: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent execution failed: JSON error");
            return Result<RawOutput>.Fail($"JSON error: {ex.Message}");
        }
    }

    public async Task<Result<RawOutput>> RewriteAsync(
        RawOutput previousOutput,
        ValidationResult validationResult,
        NarrativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previousOutput);
        ArgumentNullException.ThrowIfNull(validationResult);
        ArgumentNullException.ThrowIfNull(context);

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug(
                "Rewriting output with {ErrorCount} errors to fix",
                validationResult.Errors.Count);

            var responses = new List<NarrativeAgentResponse>();
            var errorMessages = string.Join("; ", validationResult.ErrorMessages);

            // Réexécuter chaque agent avec les erreurs à corriger
            foreach (var (agentType, previousResponse) in previousOutput.Responses)
            {
                if (!previousResponse.Success)
                {
                    // Skip les agents qui ont déjà échoué
                    responses.Add(previousResponse);
                    continue;
                }

                var rewritePrompt = BuildRewritePrompt(
                    agentType, previousResponse.Content, errorMessages, context);

                var response = await ExecuteAgentAsync(
                    rewritePrompt, context, cancellationToken);

                responses.Add(response);
            }

            totalStopwatch.Stop();

            var output = RawOutput.Create(responses, totalStopwatch.Elapsed);

            _logger?.LogDebug("Rewrite completed in {Duration}ms", totalStopwatch.ElapsedMilliseconds);

            return Result<RawOutput>.Ok(output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // User cancellation - propagate
        }
        catch (InvalidOperationException ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent rewrite failed: invalid state");
            return Result<RawOutput>.Fail($"Rewrite invalid operation: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent rewrite failed: invalid argument");
            return Result<RawOutput>.Fail($"Rewrite invalid argument: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent rewrite failed: JSON error");
            return Result<RawOutput>.Fail($"Rewrite JSON error: {ex.Message}");
        }
    }

    /// <summary>
    /// Exécute les prompts en parallèle.
    /// </summary>
    private async Task<IReadOnlyList<NarrativeAgentResponse>> ExecuteParallelAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken)
    {
        var tasks = prompts.Prompts
            .Select(p => ExecuteAgentAsync(p, context, cancellationToken))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        return responses;
    }

    /// <summary>
    /// Exécute les prompts séquentiellement.
    /// </summary>
    private async Task<IReadOnlyList<NarrativeAgentResponse>> ExecuteSequentialAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken)
    {
        var responses = new List<NarrativeAgentResponse>();

        foreach (var prompt in prompts.Prompts)
        {
            var response = await ExecuteAgentAsync(prompt, context, cancellationToken);
            responses.Add(response);

            // Si un prompt requis échoue, arrêter
            if (!response.Success && prompt.Priority == PromptPriority.Required)
            {
                _logger?.LogWarning(
                    "Required agent {Agent} failed, stopping execution",
                    prompt.TargetAgent);
                break;
            }
        }

        return responses;
    }

    /// <summary>
    /// Exécute les prompts de manière conditionnelle.
    /// </summary>
    private async Task<IReadOnlyList<NarrativeAgentResponse>> ExecuteConditionalAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken)
    {
        var responses = new List<NarrativeAgentResponse>();
        var previousResults = new Dictionary<AgentType, bool>();

        foreach (var prompt in prompts.Prompts)
        {
            // Vérifier si le prompt doit être exécuté
            bool shouldExecute = prompt.Priority switch
            {
                PromptPriority.Required => true,
                PromptPriority.Optional => previousResults.Values.All(v => v),
                PromptPriority.Fallback => previousResults.Values.Any(v => !v),
                _ => true
            };

            if (shouldExecute)
            {
                var response = await ExecuteAgentAsync(prompt, context, cancellationToken);
                responses.Add(response);
                previousResults[prompt.TargetAgent] = response.Success;
            }
            else
            {
                // Créer une réponse "skipped"
                responses.Add(new NarrativeAgentResponse(
                    prompt.TargetAgent,
                    string.Empty,
                    Success: true,
                    ErrorMessage: null,
                    TimeSpan.Zero,
                    new Dictionary<string, object> { ["skipped"] = true }));
            }
        }

        return responses;
    }

    /// <summary>
    /// Exécute un seul agent.
    /// </summary>
    private async Task<NarrativeAgentResponse> ExecuteAgentAsync(
        AgentPrompt prompt,
        NarrativeContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = new LlmRequest(
                prompt.SystemPrompt,
                prompt.UserPrompt,
                LlmParameters.Default,
                new Dictionary<string, object>
                {
                    ["agentType"] = prompt.TargetAgent.ToString(),
                    ["contextId"] = context.ContextId.Value
                });

            var result = await _llmClient.GenerateAsync(request, cancellationToken);
            stopwatch.Stop();

            if (result is Result<LlmResponse>.Success success)
            {
                return NarrativeAgentResponse.CreateSuccess(
                    prompt.TargetAgent,
                    success.Value.Content,
                    stopwatch.Elapsed)
                    .WithMetadata("tokens", success.Value.TotalTokens);
            }
            else if (result is Result<LlmResponse>.Failure failure)
            {
                return NarrativeAgentResponse.CreateFailure(
                    prompt.TargetAgent,
                    failure.Message,
                    stopwatch.Elapsed);
            }

            return NarrativeAgentResponse.CreateFailure(
                prompt.TargetAgent,
                "Unknown error",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // User cancellation - propagate
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Agent {Agent} execution failed: invalid operation", prompt.TargetAgent);
            return NarrativeAgentResponse.CreateFailure(prompt.TargetAgent, $"Invalid operation: {ex.Message}", stopwatch.Elapsed);
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Agent {Agent} execution failed: invalid argument", prompt.TargetAgent);
            return NarrativeAgentResponse.CreateFailure(prompt.TargetAgent, $"Invalid argument: {ex.Message}", stopwatch.Elapsed);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Agent {Agent} execution failed: network error", prompt.TargetAgent);
            return NarrativeAgentResponse.CreateFailure(prompt.TargetAgent, $"Network error: {ex.Message}", stopwatch.Elapsed);
        }
        catch (System.Text.Json.JsonException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Agent {Agent} execution failed: JSON error", prompt.TargetAgent);
            return NarrativeAgentResponse.CreateFailure(prompt.TargetAgent, $"JSON error: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Construit le prompt de réécriture.
    /// </summary>
    private AgentPrompt BuildRewritePrompt(
        AgentType agentType,
        string previousContent,
        string errors,
        NarrativeContext context)
    {
        var systemPrompt = $"""
            You are correcting a previous generation that had errors.
            Fix the issues while maintaining the narrative quality.
            """;

        var userPrompt = $"""
            ## Previous Output (with errors):
            {previousContent}

            ## Errors to Fix:
            {errors}

            ## Instructions:
            Rewrite the content to fix the identified errors.
            Maintain the same narrative intent and style.
            Ensure consistency with the story context.
            """;

        return AgentPrompt.Create(agentType, systemPrompt, userPrompt);
    }
}

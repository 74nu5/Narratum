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
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent execution failed");
            return Result<RawOutput>.Fail($"Agent execution failed: {ex.Message}");
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

            var responses = new List<AgentResponse>();
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
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            _logger?.LogError(ex, "Agent rewrite failed");
            return Result<RawOutput>.Fail($"Agent rewrite failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Exécute les prompts en parallèle.
    /// </summary>
    private async Task<IReadOnlyList<AgentResponse>> ExecuteParallelAsync(
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
    private async Task<IReadOnlyList<AgentResponse>> ExecuteSequentialAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken)
    {
        var responses = new List<AgentResponse>();

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
    private async Task<IReadOnlyList<AgentResponse>> ExecuteConditionalAsync(
        PromptSet prompts,
        NarrativeContext context,
        CancellationToken cancellationToken)
    {
        var responses = new List<AgentResponse>();
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
                responses.Add(new AgentResponse(
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
    private async Task<AgentResponse> ExecuteAgentAsync(
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
                return AgentResponse.CreateSuccess(
                    prompt.TargetAgent,
                    success.Value.Content,
                    stopwatch.Elapsed)
                    .WithMetadata("tokens", success.Value.TotalTokens);
            }
            else if (result is Result<LlmResponse>.Failure failure)
            {
                return AgentResponse.CreateFailure(
                    prompt.TargetAgent,
                    failure.Message,
                    stopwatch.Elapsed);
            }

            return AgentResponse.CreateFailure(
                prompt.TargetAgent,
                "Unknown error",
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Agent {Agent} execution failed", prompt.TargetAgent);

            return AgentResponse.CreateFailure(
                prompt.TargetAgent,
                ex.Message,
                stopwatch.Elapsed);
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

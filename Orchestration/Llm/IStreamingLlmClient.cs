namespace Narratum.Orchestration.Llm;

/// <summary>
/// Optional streaming capability for LLM clients. Clients that can produce output
/// incrementally implement this in addition to <see cref="ILlmClient"/>.
/// Consumers should check <c>client is IStreamingLlmClient</c> and fall back to
/// <see cref="ILlmClient.GenerateAsync"/> when streaming is not available.
/// </summary>
public interface IStreamingLlmClient
{
    /// <summary>
    /// Generates a response incrementally, yielding text fragments as they are produced.
    /// The concatenation of all yielded fragments is the full response text.
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamingAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);
}

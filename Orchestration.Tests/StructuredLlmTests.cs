using FluentAssertions;

using Narratum.Core;
using Narratum.Orchestration.Llm;

using Xunit;

namespace Narratum.Orchestration.Tests;

public sealed class StructuredLlmTests
{
    private sealed record Choice(string Text, string Description);

    [Fact]
    public void ExtractJson_StripsMarkdownFence()
    {
        const string raw = "```json\n{\"Text\":\"Avancer\",\"Description\":\"Aller de l'avant\"}\n```";

        var json = StructuredLlm.ExtractJson(raw);

        json.Should().Be("{\"Text\":\"Avancer\",\"Description\":\"Aller de l'avant\"}");
    }

    [Fact]
    public void ExtractJson_IgnoresSurroundingProse()
    {
        const string raw = "Bien sûr ! Voici le résultat : [ {\"Text\":\"A\",\"Description\":\"d\"} ] Voilà.";

        var json = StructuredLlm.ExtractJson(raw);

        json.Should().Be("[ {\"Text\":\"A\",\"Description\":\"d\"} ]");
    }

    [Fact]
    public void ExtractJson_ReturnsNull_WhenNoJsonPresent()
    {
        StructuredLlm.ExtractJson("désolé, je ne peux pas").Should().BeNull();
        StructuredLlm.ExtractJson("   ").Should().BeNull();
        StructuredLlm.ExtractJson(null).Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_ParsesTypedRecordFromNoisyText()
    {
        const string raw = "```json\n{\"text\":\"Fuir\",\"description\":\"Prendre la fuite\"}\n```";

        var ok = StructuredLlm.TryDeserialize<Choice>(raw, out var choice);

        ok.Should().BeTrue();
        choice!.Text.Should().Be("Fuir");
        choice.Description.Should().Be("Prendre la fuite");
    }

    [Fact]
    public void BuildSchema_DescribesTargetType()
    {
        var schema = StructuredLlm.BuildSchema<Choice>();

        // Web defaults => camelCase property names in the schema.
        schema.Should().Contain("text").And.Contain("description");
    }

    [Fact]
    public async Task GenerateViaPromptAsync_InjectsSchemaAndParses()
    {
        var fake = new FakeLlmClient(_ =>
            "Voici : {\"text\":\"Attendre\",\"description\":\"Patienter un tour\"}");
        ILlmClient client = fake;

        var request = new LlmRequest("Tu es un narrateur.", "Propose une action.");
        var result = await client.GenerateStructuredAsync<Choice>(request);

        result.Should().BeOfType<Result<Choice>.Success>();
        ((Result<Choice>.Success)result).Value.Text.Should().Be("Attendre");
        fake.LastSystemPrompt.Should().Contain("schéma", "the schema must be injected into the prompt");
    }

    [Fact]
    public async Task GenerateViaPromptAsync_RetriesThenFails_OnUnparseableOutput()
    {
        var fake = new FakeLlmClient(_ => "je ne sais pas répondre en JSON");
        ILlmClient client = fake;

        var request = new LlmRequest("Système.", "Utilisateur.");
        var result = await client.GenerateStructuredAsync<Choice>(request);

        result.Should().BeOfType<Result<Choice>.Failure>();
        fake.CallCount.Should().Be(2, "the default path retries once before giving up");
    }

    /// <summary>Minimal ILlmClient returning canned text, to drive the default structured path.</summary>
    private sealed class FakeLlmClient(Func<LlmRequest, string> respond) : ILlmClient
    {
        public int CallCount { get; private set; }
        public string? LastSystemPrompt { get; private set; }

        public Task<Result<LlmResponse>> GenerateAsync(
            LlmRequest request, CancellationToken cancellationToken = default)
        {
            this.CallCount++;
            this.LastSystemPrompt = request.SystemPrompt;
            var response = new LlmResponse(request.RequestId, respond(request));
            return Task.FromResult(Result<LlmResponse>.Ok(response));
        }

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string ClientName => "Fake";
    }
}

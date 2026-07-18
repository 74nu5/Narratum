using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

using Narratum.Core;

namespace Narratum.Orchestration.Llm;

/// <summary>
/// Utilitaires de sortie structurée (JSON typé) pour les clients LLM.
///
/// Deux usages :
/// - <see cref="GenerateViaPromptAsync{T}"/> : chemin par défaut réutilisable par n'importe
///   quel <see cref="ILlmClient"/> qui n'a pas de support natif du schéma — on injecte le
///   schéma dans le prompt puis on parse de façon tolérante, avec une nouvelle tentative.
/// - <see cref="BuildSchema{T}"/> / <see cref="TryDeserialize{T}"/> : briques partagées qu'un
///   adaptateur avec support natif (schéma strict) réutilise pour son repli tolérant.
///
/// La sortie stricte n'étant pas fiable avec les petits modèles locaux, le parseur tolérant
/// est toujours présent en filet.
/// </summary>
public static class StructuredLlm
{
    // An explicit reflection resolver is required: schema export and (de)serialization throw
    // "must specify a TypeInfoResolver" when reflection-based serialization is disabled by default,
    // which is the case in the Blazor Server host (it is enabled in the test host, hence the gap).
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>Nombre total de tentatives du chemin par défaut (1 essai + 1 renfort).</summary>
    private const int MaxAttempts = 2;

    /// <summary>Dérive une représentation JSON Schema du type cible.</summary>
    public static string BuildSchema<T>()
        => SerializerOptions.GetJsonSchemaAsNode(typeof(T)).ToJsonString();

    /// <summary>
    /// Tente de désérialiser <typeparamref name="T"/> depuis une réponse LLM potentiellement
    /// bruitée (texte autour, balises Markdown). Retourne false plutôt que de lever.
    /// </summary>
    public static bool TryDeserialize<T>(string? raw, out T? value)
    {
        value = default;

        var json = ExtractJson(raw);
        if (json is null)
            return false;

        try
        {
            value = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Isole le fragment JSON le plus plausible d'une réponse : retire les clôtures Markdown
    /// puis renvoie le premier objet <c>{…}</c> ou tableau <c>[…]</c> équilibré. Null si absent.
    /// </summary>
    public static string? ExtractJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();

        // Retire une clôture de code Markdown (```json … ```), fréquente en sortie de modèle.
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];

            var closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
                text = text[..closingFence];

            text = text.Trim();
        }

        var braceIndex = text.IndexOf('{');
        var bracketIndex = text.IndexOf('[');

        char closing;
        int start;
        if (bracketIndex >= 0 && (braceIndex < 0 || bracketIndex < braceIndex))
        {
            start = bracketIndex;
            closing = ']';
        }
        else if (braceIndex >= 0)
        {
            start = braceIndex;
            closing = '}';
        }
        else
        {
            return null;
        }

        var end = text.LastIndexOf(closing);
        return end > start ? text[start..(end + 1)] : null;
    }

    /// <summary>
    /// Chemin par défaut : ajoute le schéma au prompt système, génère, puis parse de façon
    /// tolérante. En cas d'échec, renforce l'instruction et retente une fois.
    /// </summary>
    public static async Task<Result<T>> GenerateViaPromptAsync<T>(
        ILlmClient client,
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        var schema = BuildSchema<T>();
        var lastError = "no attempt made";

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var reinforcement = attempt == 1
                ? string.Empty
                : "\n\nATTENTION : la réponse précédente n'était pas un JSON valide. " +
                  "Réponds cette fois avec le JSON SEUL, sans aucun autre texte.";

            var systemPrompt =
                request.SystemPrompt +
                "\n\nRéponds UNIQUEMENT avec un JSON valide conforme à ce schéma, " +
                "sans texte explicatif ni balises Markdown :\n" + schema + reinforcement;

            var structuredRequest = new LlmRequest(
                systemPrompt,
                request.UserPrompt,
                request.Parameters,
                request.Metadata);

            var result = await client.GenerateAsync(structuredRequest, cancellationToken);

            switch (result)
            {
                case Result<LlmResponse>.Success success
                    when TryDeserialize<T>(success.Value.Content, out var value):
                    return Result<T>.Ok(value!);

                case Result<LlmResponse>.Success:
                    lastError = "response was not valid JSON for the expected schema";
                    break;

                case Result<LlmResponse>.Failure failure:
                    lastError = failure.Message;
                    break;
            }
        }

        return Result<T>.Fail($"Structured generation failed after {MaxAttempts} attempts: {lastError}");
    }
}

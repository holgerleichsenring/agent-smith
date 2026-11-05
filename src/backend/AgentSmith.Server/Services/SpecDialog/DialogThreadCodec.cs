using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Writes a dialog thread for the database and reads it back: per message its role, its texts, its tool calls (id,
/// name, arguments) and its tool results (call id, text). Every text and result is masked with the provider
/// placeholder, so what is at rest is what a provider was allowed to see. An image becomes a one-line marker, any
/// other content (reasoning first) is dropped, and so is a call without its result or a result without its call —
/// a provider refuses a thread that carries either.
/// </summary>
public sealed class DialogThreadCodec(SecretMasker masker)
{
    public const string ImageMarker = "[An image was attached here; it is not kept between turns.]";

    public string Encode(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var paired = Paired(messages);
        var array = new JsonArray();
        foreach (var message in messages)
            if (Parts(message, paired) is { Count: > 0 } parts)
                array.Add(new JsonObject { ["role"] = message.Role.Value, ["parts"] = parts });
        return array.ToJsonString();
    }

    public IReadOnlyList<ChatMessage> Decode(string json) =>
        [.. JsonNode.Parse(json)!.AsArray().Select(m => new ChatMessage(
            new ChatRole(m!["role"]!.GetValue<string>()), [.. m["parts"]!.AsArray().Select(p => Content(p!))]))];

    private JsonArray Parts(ChatMessage message, IReadOnlySet<string> paired)
    {
        var parts = new JsonArray();
        foreach (var content in message.Contents)
            if (Part(content, paired) is { } part) parts.Add(part);
        return parts;
    }

    private JsonObject? Part(AIContent content, IReadOnlySet<string> paired) => content switch
    {
        TextContent text when !string.IsNullOrEmpty(text.Text) => Text(masker.Apply(text.Text, SecretMasker.ProviderPlaceholder)),
        DataContent data when data.HasTopLevelMediaType("image") => Text(ImageMarker),
        UriContent uri when uri.HasTopLevelMediaType("image") => Text(ImageMarker),
        FunctionCallContent call when paired.Contains(call.CallId) => new JsonObject
        {
            ["call"] = call.CallId, ["name"] = call.Name,
            ["arguments"] = JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>()),
        },
        FunctionResultContent result when paired.Contains(result.CallId) => new JsonObject
        {
            ["result"] = result.CallId,
            ["text"] = masker.Apply(ResultText(result.Result), SecretMasker.ProviderPlaceholder),
        },
        _ => null,
    };

    private static JsonObject Text(string text) => new() { ["text"] = text };

    private static AIContent Content(JsonNode part)
    {
        if (part["call"] is { } call)
            return new FunctionCallContent(call.GetValue<string>(), part["name"]!.GetValue<string>(),
                JsonSerializer.Deserialize<Dictionary<string, object?>>(part["arguments"]!.GetValue<string>()));
        if (part["result"] is { } result)
            return new FunctionResultContent(result.GetValue<string>(), part["text"]!.GetValue<string>());
        return new TextContent(part["text"]!.GetValue<string>());
    }

    // A call id that both a call and a result carry; every other call and result is an orphan.
    private static HashSet<string> Paired(IReadOnlyList<ChatMessage> messages)
    {
        var contents = messages.SelectMany(m => m.Contents).ToList();
        var calls = contents.OfType<FunctionCallContent>().Select(c => c.CallId).ToHashSet(StringComparer.Ordinal);
        return [.. contents.OfType<FunctionResultContent>().Select(r => r.CallId).Where(calls.Contains)];
    }

    private static string ResultText(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        _ => JsonSerializer.Serialize(result),
    };
}

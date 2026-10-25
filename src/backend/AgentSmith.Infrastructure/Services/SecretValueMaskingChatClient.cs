using System.Text.Json;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-10-25-c5a7i: no secret value the server knows reaches a model provider. Every outbound
/// message's text and tool results are masked with <see cref="SecretMasker.ProviderPlaceholder"/>,
/// on a copy: the tool loop's own list keeps the raw text. The current turn's result of a
/// <see cref="SensitiveToolNames"/> tool passes unmasked, the rule the history scrub keeps, so a
/// tool that hands the model a credential on purpose still does. A message with nothing to mask is
/// passed as the same instance, so prompt-cache anchors on it are untouched.
/// </summary>
internal sealed class SecretValueMaskingChatClient(IChatClient inner, SecretMasker masker) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(Mask(messages.ToList()), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(Mask(messages.ToList()), options, cancellationToken);

    private List<ChatMessage> Mask(List<ChatMessage> messages)
    {
        var freshSensitive = FreshSensitiveCallIds(messages);
        return [.. messages.Select(message => Mask(message, freshSensitive))];
    }

    private ChatMessage Mask(ChatMessage message, IReadOnlySet<string> freshSensitive)
    {
        var contents = message.Contents.Select(content => Mask(content, freshSensitive)).ToList();
        if (contents.SequenceEqual(message.Contents, ReferenceEqualityComparer.Instance)) return message;
        return new ChatMessage(message.Role, contents)
        {
            AuthorName = message.AuthorName,
            MessageId = message.MessageId,
            AdditionalProperties = message.AdditionalProperties,
            RawRepresentation = message.RawRepresentation,
        };
    }

    private AIContent Mask(AIContent content, IReadOnlySet<string> freshSensitive) => content switch
    {
        TextContent text when Masked(text.Text) is { } masked =>
            new TextContent(masked) { AdditionalProperties = text.AdditionalProperties },
        FunctionResultContent result when !freshSensitive.Contains(result.CallId)
            && Masked(ResultText(result.Result)) is { } masked =>
            new FunctionResultContent(result.CallId, masked) { AdditionalProperties = result.AdditionalProperties },
        _ => content,
    };

    private string? Masked(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var masked = masker.Apply(text, SecretMasker.ProviderPlaceholder);
        return ReferenceEquals(masked, text) || masked == text ? null : masked;
    }

    private static string? ResultText(object? result) => result switch
    {
        null => null,
        string text => text,
        _ => JsonSerializer.Serialize(result),
    };

    // A sensitive tool's result is fresh while no assistant message follows it.
    private static HashSet<string> FreshSensitiveCallIds(List<ChatMessage> messages)
    {
        var sensitive = messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .Where(call => SensitiveToolNames.All.Contains(call.Name)).Select(call => call.CallId)
            .ToHashSet(StringComparer.Ordinal);
        var lastAssistant = messages.FindLastIndex(m => m.Role == ChatRole.Assistant);
        return [.. messages.Skip(lastAssistant + 1).SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(result => result.CallId).Where(sensitive.Contains)];
    }
}

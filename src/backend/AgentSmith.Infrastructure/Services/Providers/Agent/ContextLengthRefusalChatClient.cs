using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-08-27-3eb1: turns a provider's context-length refusal into a message that names
/// the role, the window it was run against and the setting that would have prevented it.
/// The bare provider text ("context_length_exceeded … 146231 tokens > 128000 maximum")
/// says nothing about WHICH of an agent's seven model roles produced it, and a run that
/// died four times in a row on it left the operator with no setting to change.
///
/// <para>Classified on the provider's error text, not on a type: neither the OpenAI nor
/// the Anthropic SDK gives this refusal a distinct exception type, the same reason
/// <c>TransientRetryChatClient</c> matches <c>invalid_request_error</c> by text.</para>
/// </summary>
internal sealed class ContextLengthRefusalChatClient(
    IChatClient inner, string role, string model, int? windowTokens, ILogger? logger = null)
    : DelegatingChatClient(inner)
{
    private static readonly string[] Markers =
    [
        "context_length_exceeded", "context length exceeded", "maximum context length",
        "prompt is too long", "too many total text bytes",
    ];

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? messages.ToList();
        try
        {
            return await base.GetResponseAsync(list, options, cancellationToken);
        }
        catch (Exception ex) when (IsContextLengthRefusal(ex))
        {
            var message = Explain(role, model, windowTokens, CompactingChatClient.EstimateTokens(list));
            logger?.LogError(ex, "{Message}", message);
            throw new InvalidOperationException(message, ex);
        }
    }

    /// <summary>True when the exception chain carries a provider context-length refusal.</summary>
    internal static bool IsContextLengthRefusal(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
            foreach (var marker in Markers)
                if (e.Message.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    /// <summary>The operator-facing explanation: which role, which window, which setting.</summary>
    internal static string Explain(string role, string model, int? windowTokens, int estimatedTokens) =>
        $"The '{role}' model role ({model}) was refused for context length at roughly "
        + $"{estimatedTokens} estimated input tokens. "
        + (windowTokens is { } window
            ? $"Its stated window is {window} tokens, so the conversation outgrew it: lower "
              + $"agents.<name>.compaction.max_context_tokens (and keep max_context_tokens_trigger_ratio "
              + $"below 1) so the fold happens before the provider refuses."
            : "No window is stated for this role, so nothing could fold before the provider "
              + "refused: set models.<role>.context_window_tokens to the deployment's input "
              + "limit — the model NAME does not imply it.");
}

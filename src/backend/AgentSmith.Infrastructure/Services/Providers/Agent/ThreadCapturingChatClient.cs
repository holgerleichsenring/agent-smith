using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Records each call's forwarded messages and response into a <see cref="ForwardedThreadCapture"/>. Placed directly
/// under the compactor, so what it records is the view the provider was sent — folded when the loop folded — and not
/// the pressure finaliser's instruction or the scrub and mask rewrites below it.
/// </summary>
public sealed class ThreadCapturingChatClient(IChatClient inner, ForwardedThreadCapture capture) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var sent = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var response = await base.GetResponseAsync(sent, options, cancellationToken);
        capture.Record(sent, response);
        return response;
    }
}
